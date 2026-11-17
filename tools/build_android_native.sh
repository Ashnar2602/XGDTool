#!/usr/bin/env bash
# Build the current C++ sources before Gradle packages the Android application.
set -euo pipefail

fail() {
    printf 'Android native build: %s\n' "$*" >&2
    exit 1
}

[[ "$(uname -s)" == Linux ]] || fail 'This script requires a Linux build host.'
[[ "$(uname -m)" == x86_64 ]] || fail 'This script requires an x86_64 build host.'
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd -- "$repo_root"
for command in git cmake ninja sha256sum realpath; do
    command -v "$command" >/dev/null || fail "Missing required command: $command"
done

# The gitlinks in this checkout pin both dependencies and their build recipes.
for submodule in external/vcpkg external/ZArchive external/cmake-bin2h; do
    [[ -e "$submodule/.git" ]] || fail "Initialize recursive submodules: $submodule"
    expected="$(git rev-parse "HEAD:$submodule")"
    actual="$(git -C "$submodule" rev-parse HEAD)"
    [[ "$actual" == "$expected" ]] || fail "Submodule does not match its pinned gitlink: $submodule"
done

ndk_version=27.2.12479018
sdk_root="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}"
ndk_root="${ANDROID_NDK_HOME:-${sdk_root:+$sdk_root/ndk/$ndk_version}}"
[[ -n "$ndk_root" && -f "$ndk_root/source.properties" ]] || \
    fail "Install NDK $ndk_version and set ANDROID_NDK_HOME or ANDROID_SDK_ROOT."
ndk_root="$(cd -- "$ndk_root" && pwd -P)"
grep -Eq '^Pkg.Revision[[:space:]]*=[[:space:]]*27\.2\.12479018[[:space:]]*$' \
    "$ndk_root/source.properties" || fail "NDK $ndk_version is required."
export ANDROID_NDK_HOME="$ndk_root"
export ANDROID_NDK_ROOT="$ndk_root"
llvm_bin="$ndk_root/toolchains/llvm/prebuilt/linux-x86_64/bin"
[[ -x "$llvm_bin/llvm-strip" && -x "$llvm_bin/llvm-readelf" ]] || \
    fail 'The installed NDK does not contain the Linux LLVM tools.'

build_dir="$repo_root/build/android-native-arm64"
deps_root="$build_dir/vcpkg-installed"
destination="$repo_root/android/app/src/main/jniLibs/arm64-v8a/libxgdtool.so"
for output in "$build_dir" "$destination"; do
    case "$(realpath -m -- "$output")" in
        "$repo_root"/*) ;;
        *) fail "Output path leaves the repository: $output" ;;
    esac
done
mkdir -p -- "$build_dir"
export VCPKG_DOWNLOADS="$build_dir/vcpkg-downloads"
export VCPKG_DEFAULT_BINARY_CACHE="$build_dir/vcpkg-cache"
mkdir -p -- "$VCPKG_DOWNLOADS" "$VCPKG_DEFAULT_BINARY_CACHE"

# Accept only the exact checked-in patch, whether fresh or already applied.
patch_file="$repo_root/cmake/zarchive_multithread.patch"
if git -C external/ZArchive apply --reverse --check "$patch_file" 2>/dev/null; then
    printf '%s\n' 'ZArchive multithread patch already applied.'
else
    git -C external/ZArchive apply --check "$patch_file"
    git -C external/ZArchive apply "$patch_file"
fi
grep -q 'numThreads' external/ZArchive/include/zarchive/zarchivewriter.h || \
    fail 'ZArchive multithread support was not applied.'

./external/vcpkg/bootstrap-vcpkg.sh -disableMetrics
./external/vcpkg/vcpkg install --triplet arm64-android \
    --x-install-root="$deps_root" \
    'curl[core,openssl]' openssl zstd lz4 nlohmann-json

cmake -S android/jni -B "$build_dir/cmake" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE="$repo_root/external/vcpkg/scripts/buildsystems/vcpkg.cmake" \
    -DVCPKG_CHAINLOAD_TOOLCHAIN_FILE="$ndk_root/build/cmake/android.toolchain.cmake" \
    -DVCPKG_TARGET_TRIPLET=arm64-android \
    -DVCPKG_INSTALLED_DIR="$deps_root" \
    -DANDROID_ABI=arm64-v8a \
    -DANDROID_PLATFORM=android-26 \
    -DANDROID_STL=c++_static \
    -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
    -DXGD_USE_VCPKG=ON
cmake --build "$build_dir/cmake" --target xgdtool --parallel "${CMAKE_BUILD_PARALLEL_LEVEL:-2}"

native_lib="$build_dir/cmake/libxgdtool.so"
[[ -s "$native_lib" ]] || fail 'The native build did not produce libxgdtool.so.'
"$llvm_bin/llvm-readelf" --file-header "$native_lib" > "$build_dir/elf-header.txt"
"$llvm_bin/llvm-readelf" --dynamic "$native_lib" > "$build_dir/elf-dynamic.txt"
grep -Eq 'Class:[[:space:]]+ELF64' "$build_dir/elf-header.txt" || fail 'Expected an ELF64 library.'
grep -Eq 'Machine:[[:space:]]+AArch64' "$build_dir/elf-header.txt" || fail 'Expected an ARM64 library.'

# All third-party libraries and libc++ must be static. These are Android's
# public system libraries; a host library or unbundled DSO fails the build.
while IFS= read -r needed; do
    case "$needed" in
        libc.so|libm.so|libdl.so|liblog.so|libz.so|libandroid.so) ;;
        *) fail "Unexpected shared dependency: $needed" ;;
    esac
done < <(sed -n 's/.*(NEEDED).*\[\([^]]*\)\].*/\1/p' "$build_dir/elf-dynamic.txt")

staged_lib="$build_dir/libxgdtool.stripped.so"
"$llvm_bin/llvm-strip" --strip-unneeded "$native_lib" -o "$staged_lib"
mkdir -p -- "$(dirname -- "$destination")"
cp -- "$staged_lib" "$destination"
sha256sum "$destination" | tee "$build_dir/packaged-library.sha256"
printf '%s\n' 'Native library rebuilt and staged. Run android/gradlew assembleRelease next.'
