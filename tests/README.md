# Proposed directory-enumeration regression test

Destination in XGDTool: `tests/test_directory_entries.py`.

The test compiles the actual directory traversal/public wrapper bodies extracted
from `src/ImageReader/ImageReader.cpp`, includes the real `ImageReader.h` and
`Formats/Xiso.h`, and supplies a bounds-checked in-memory reader. It does not
copy the traversal into the test, require a game image, or build the application.
The only unrelated seams are unused file-time initialization and error logging.
The case-insensitive executable matcher also uses the actual production bodies.

Seven cases cover a valid `left_offset=0x00FF` and its entire subtree, actual
`0xFFFF` padding, a normal leaf, a nested directory, executable-only selection,
full byte counts/cache reuse, and a nonzero partition offset. Names, sizes and
types are checked against literal expected maps independent of the traversal.

With MSVC already configured by the existing release workflow:

```powershell
python tests/test_directory_entries.py --source-root . --output-dir build_directory_regression
```

The standalone test also supports a C++20 `c++`, `g++` or `clang++` host compiler.
No third-party test framework or dependency download is required. Add the test
step after the workflow's MSVC setup and before dependency installation to fail
quickly; optionally run the same command on Linux. Python/compilers are already
present in the hosted runners. Keep the normal GUI/CLI/Android CI builds.

Expected behavior on unchanged 1.4.2: valid `0x00FF` cases fail, and the true
`0xFFFF` padding case also detects the wrong comparison. With only the padding
comparison corrected to `PAD_SHORT`, all seven should pass. The generated
fixture, compiler log, test log and source hashes remain in the chosen output
directory for inspection. These are metadata tests, not a general writer or
all-formats integrity certificate.

No files in the XGDTool repository were changed by preparation of this proposal.
Full application compilation remains delegated to the user's CI as requested.

## Standalone CMake and CTest

The test directory is an independent CMake project; it does not configure the
full application's GUI or third-party dependencies. From the repository root:

```sh
cmake -S tests -B build_directory_regression -DCMAKE_BUILD_TYPE=Release
cmake --build build_directory_regression --config Release
ctest --test-dir build_directory_regression -C Release --output-on-failure
```

On Windows run those commands from an MSVC developer prompt. They also work
with a C++20 compiler on Linux. CTest runs the seven named cases separately.
The compiler uses `/W4 /WX` (MSVC) or `-Wall -Wextra -Werror` (other compilers).

For generation alone, without even requiring a compiler:

```sh
python tests/test_directory_entries.py --source-root . --output-dir build_directory_fixture --emit-only
```

The emit-only receipt has `passed: null`; generating a fixture is not a test
pass. CTest or the standalone script must compile and execute it to qualify
the result.
