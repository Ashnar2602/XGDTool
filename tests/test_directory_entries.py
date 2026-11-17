#!/usr/bin/env python3
"""Regress ImageReader's real directory method using synthetic XDVDFS metadata.

Run in a compiler-enabled shell (MSVC developer prompt or C++20 c++/clang++):
  python tests/test_directory_entries.py --source-root .

No application build, dependencies, disc image, network or game assets required.
The fixture includes the production ImageReader/Xiso headers and extracts the
named production method bodies unchanged. Only byte storage, unused FileTime
initialization and exception logging are test seams. The expected tree is
specified independently below, not derived by another directory traversal.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import tempfile


def production_function(source, signature):
    """Extract one complete function; fail if its signature becomes ambiguous."""
    if source.count(signature) != 1:
        raise RuntimeError(f"Expected one production function: {signature}")
    start = source.index(signature)
    brace = source.index("{", start)
    level = 1
    index = brace + 1
    state = "code"
    while index < len(source):
        char = source[index]
        after = source[index:index + 2]
        if state == "line":
            if char == "\n":
                state = "code"
        elif state == "block":
            if after == "*/":
                state = "code"
                index += 1
        elif state in ('"', "'"):
            if char == "\\":
                index += 1
            elif char == state:
                state = "code"
        elif after == "//":
            state = "line"
            index += 1
        elif after == "/*":
            state = "block"
            index += 1
        elif char in ('"', "'"):
            state = char
        elif char == "{":
            level += 1
        elif char == "}":
            level -= 1
            if not level:
                return source[start:index + 1]
        index += 1
    raise RuntimeError(f"Unterminated production function: {signature}")


FIXTURE = r'''
#include <algorithm>
#include <array>
#include <cctype>
#include <cstring>
#include <filesystem>
#include <iostream>
#include <map>
#include <memory>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>
#if defined(_MSC_VER)
// Existing production headers pack STL-bearing DirectoryEntry, and the
// production lowercase helper assigns tolower(int) to char. Suppress only
// those pre-existing diagnostics while compiling unmodified production text;
// the synthetic harness below still uses /W4 /WX without suppression.
#pragma warning(push)
#pragma warning(disable: 4315 4244)
#endif
#include "ImageReader/ImageReader.h"
#include "Utils/StringUtils.h"

// Neither member participates in traversal. Keep unrelated logging/time
// dependencies out of this dependency-free metadata regression fixture.
void XGDException::log_error(Code, const std::string&, const std::string&) {}
Xiso::FileTime::FileTime() : low(0), high(0) {}

@PRODUCTION_METHODS@
#if defined(_MSC_VER)
#pragma warning(pop)
#endif

static void require(bool condition, const std::string& message) {
  if (!condition) throw std::runtime_error(message);
}

class MemoryReader final : public ImageReader {
 public:
  static constexpr uint32_t kRootSector = 40;
  static constexpr uint32_t kNestedSector = 42;
  std::vector<char> bytes;
  size_t reads = 0;
  explicit MemoryReader(uint64_t partition_offset = 0)
      : bytes(size_t(partition_offset) + 48 * Xiso::SECTOR_SIZE, 0),
        partition_offset_(partition_offset) {
    const std::array<uint32_t, 2> root = {kRootSector, uint32_t(Xiso::SECTOR_SIZE)};
    std::memcpy(bytes.data() + partition_offset_ + Xiso::MAGIC_OFFSET +
                Xiso::MAGIC_DATA_LEN, root.data(), sizeof(root));
  }
  uint64_t image_offset() override { return partition_offset_; }
  uint32_t total_sectors() override { return 48; }
  std::string name() override { return "synthetic-directory"; }
  void read_sector(uint32_t sector, char* out) override {
    read_bytes(partition_offset_ + uint64_t(sector) * Xiso::SECTOR_SIZE,
               Xiso::SECTOR_SIZE, out);
  }
  void read_bytes(uint64_t offset, size_t count, char* out) override {
    require(offset <= bytes.size() && count <= bytes.size() - offset,
            "Traversal attempted an out-of-bounds synthetic read");
    if (count) std::memcpy(out, bytes.data() + size_t(offset), count);
    ++reads;
  }
  void entry(uint32_t sector, uint16_t ordinal, const std::string& name,
             uint16_t left, uint16_t right, uint32_t size,
             uint8_t attributes = Xiso::ATTRIBUTE_NORMAL,
             uint32_t start_sector = 45) {
    require(name.size() <= 255, "Fixture name is too long");
    const uint64_t offset = partition_offset_ + uint64_t(sector) *
                            Xiso::SECTOR_SIZE + uint64_t(ordinal) * 4;
    const Xiso::DirectoryEntry::Header header = {
        left, right, start_sector, size, attributes, uint8_t(name.size())};
    require(offset + sizeof(header) + name.size() <= bytes.size(),
            "Fixture entry is out of bounds");
    std::memcpy(bytes.data() + offset, &header, sizeof(header));
    std::memcpy(bytes.data() + offset + sizeof(header), name.data(), name.size());
  }
  void padding(uint32_t sector, uint16_t ordinal) {
    // Real padding has a 16-bit FFFF sentinel. Poison right_offset as well:
    // correct code must skip before trying to enqueue either child.
    const uint64_t offset = partition_offset_ + uint64_t(sector) *
                            Xiso::SECTOR_SIZE + uint64_t(ordinal) * 4;
    require(offset + sizeof(Xiso::DirectoryEntry::Header) + 255 <= bytes.size(),
            "Fixture padding is out of bounds");
    std::memset(bytes.data() + offset, 0xFF,
                sizeof(Xiso::DirectoryEntry::Header) + 255);
  }
  void tree(uint32_t sector) {
    // 0x00FF is a valid four-byte directory ordinal, not byte padding.
    // Root and all three descendants must survive the comparison.
    entry(sector, 0, "z-root.bin", 0x00FF, 400, 17);
    entry(sector, 0x00FF, "m-branch.bin", 300, 350, 23);
    entry(sector, 300, "Default.xex", 0, 0, 41);
    entry(sector, 350, "r-right.bin", 0, 0, 31);
    padding(sector, 400);
  }
 private:
  uint64_t partition_offset_;
};

using Expected = std::map<std::string, std::pair<uint32_t, bool>>;
static Expected expected_tree(const std::string& prefix = "") {
  return {{prefix + "Default.xex", {41, false}},
          {prefix + "m-branch.bin", {23, false}},
          {prefix + "r-right.bin", {31, false}},
          {prefix + "z-root.bin", {17, false}}};
}
static void check_entries(MemoryReader& reader, const Expected& expected) {
  const auto& entries = reader.directory_entries();
  Expected actual;
  for (const auto& entry : entries) {
    const bool directory = (entry.header.attributes & Xiso::ATTRIBUTE_DIRECTORY) != 0;
    require(actual.emplace(entry.path.generic_string(),
                           std::make_pair(entry.header.file_size, directory)).second,
            "Traversal returned a duplicate path");
  }
  require(actual == expected, "Production traversal differs from expected names/sizes/types");
  require(std::is_sorted(entries.begin(), entries.end(), [](const auto& a, const auto& b) {
    const bool ad = (a.header.attributes & Xiso::ATTRIBUTE_DIRECTORY) != 0;
    const bool bd = (b.header.attributes & Xiso::ATTRIBUTE_DIRECTORY) != 0;
    return ad != bd ? ad > bd : a.path < b.path;
  }), "Production sorting changed");
}

int main(int argc, char** argv) {
  unsigned tests = 0, failures = 0;
  auto test = [&](const char* name, auto body) {
    if (argc == 2 && std::string(argv[1]) != name) return;
    ++tests;
    try { body(); std::cout << "PASS " << name << '\n'; }
    catch (const std::exception& error) {
      ++failures; std::cerr << "FAIL " << name << ": " << error.what() << '\n';
    }
  };
  test("valid-left-00FF-retains-entire-subtree-and-ignores-FFFF", [] {
    MemoryReader reader; reader.tree(MemoryReader::kRootSector);
    check_entries(reader, expected_tree());
  });
  test("ordinary-leaf-control", [] {
    MemoryReader reader;
    reader.entry(MemoryReader::kRootSector, 0, "leaf.bin", 0, 0, 19);
    check_entries(reader, {{"leaf.bin", {19, false}}});
  });
  test("FFFF-root-padding-ignored", [] {
    MemoryReader reader; reader.padding(MemoryReader::kRootSector, 0);
    check_entries(reader, {});
  });
  test("nested-tree-preserves-directory-and-relative-paths", [] {
    MemoryReader reader;
    reader.entry(MemoryReader::kRootSector, 0, "shaders", 0, 0,
                 Xiso::SECTOR_SIZE, Xiso::ATTRIBUTE_DIRECTORY,
                 MemoryReader::kNestedSector);
    reader.tree(MemoryReader::kNestedSector);
    auto expected = expected_tree("shaders/");
    expected.emplace("shaders", std::make_pair(uint32_t(Xiso::SECTOR_SIZE), true));
    check_entries(reader, expected);
  });
  test("executable-only-reaches-valid-00FF-subtree", [] {
    MemoryReader reader; reader.tree(MemoryReader::kRootSector);
    require(reader.executable_entry().filename == "Default.xex",
            "Executable-only scan lost the subtree");
    const size_t reads = reader.reads;
    require(reader.executable_entry().header.file_size == 41 && reader.reads == reads,
            "Executable cache changed or reread the image");
  });
  test("total-bytes-and-entry-cache-retain-all-files", [] {
    MemoryReader reader; reader.tree(MemoryReader::kRootSector);
    check_entries(reader, expected_tree());
    const size_t reads = reader.reads;
    require(reader.total_file_bytes() == 112, "Input byte count omits a file");
    require(reader.total_file_bytes() == 112 && reader.directory_entries().size() == 4,
            "Cached public APIs changed their result");
    require(reader.reads == reads, "Cached public APIs reread image metadata");
  });
  test("nonzero-partition-offset", [] {
    MemoryReader reader(3 * Xiso::SECTOR_SIZE);
    reader.tree(MemoryReader::kRootSector);
    check_entries(reader, expected_tree());
  });
  require(tests != 0, "No matching test case");
  std::cout << "RESULT tests=" << tests << " failures=" << failures << '\n';
  return failures ? 1 : 0;
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path,
                        default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--emit-only", action="store_true",
                        help="Generate fixture and source receipt without requiring a compiler")
    parser.add_argument("--cxx", help="C++20 compiler command; defaults to CXX or cl/c++")
    args = parser.parse_args()
    root = args.source_root.resolve()
    source_path = root / "src/ImageReader/ImageReader.cpp"
    string_path = root / "src/Utils/StringUtils.cpp"
    source = source_path.read_text(encoding="utf-8")
    string_source = string_path.read_text(encoding="utf-8")
    signatures = [
        "void ImageReader::read_sectors(",
        "const std::vector<Xiso::DirectoryEntry>& ImageReader::directory_entries()",
        "const Xiso::DirectoryEntry& ImageReader::executable_entry()",
        "uint64_t ImageReader::total_file_bytes()",
        "void ImageReader::populate_directory_entries(bool exe_only)",
    ]
    functions = [production_function(source, signature) for signature in signatures]
    functions.append("namespace StringUtils {\n" + "\n".join([
        production_function(string_source, "std::string to_lower(const std::string& str)"),
        production_function(string_source,
                            "bool case_insensitive_search(const std::string& original, const std::string& to_find)"),
    ]) + "\n}")
    output = args.output_dir.resolve() if args.output_dir else Path(
        tempfile.mkdtemp(prefix="xgd-directory-regression-"))
    output.mkdir(parents=True, exist_ok=True)
    fixture = output / "directory_entries_test.cpp"
    fixture.write_text(FIXTURE.replace("@PRODUCTION_METHODS@", "\n\n".join(functions)),
                       encoding="utf-8")
    receipt = {
        "passed": None,
        "emitted_only": args.emit_only,
        "fixture_sha256": hashlib.sha256(fixture.read_bytes()).hexdigest(),
        "production_source_sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
        "method_sha256": {signature: hashlib.sha256(body.encode()).hexdigest()
                          for signature, body in zip(signatures, functions)},
        "limits": "Actual extracted production methods and headers; synthetic read-only metadata. No writer, compression, real ISO or application build.",
    }
    if args.emit_only:
        (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        print(f"Emitted: {fixture}; C++ tests not executed")
        return 0
    compiler = args.cxx or os.environ.get("CXX") or ("cl" if os.name == "nt" else "c++")
    command = shlex.split(compiler, posix=os.name != "nt")
    if not shutil.which(command[0]):
        parser.error(f"Compiler unavailable: {command[0]}; use a developer shell or --cxx")
    executable = output / ("directory_entries_test.exe" if os.name == "nt" else "directory_entries_test")
    if Path(command[0]).stem.lower() in ("cl", "clang-cl"):
        command += ["/nologo", "/EHsc", "/std:c++20", "/W4", "/WX", "/O2",
                    "/I" + str(root / "src"), str(fixture), "/Fe:" + str(executable),
                    "/Fo:" + str(output / "directory_entries_test.obj")]
    else:
        command += ["-std=c++20", "-Wall", "-Wextra", "-Werror", "-O2",
                    "-I", str(root / "src"), str(fixture), "-o", str(executable)]
    compiled = subprocess.run(command, cwd=output, text=True, capture_output=True)
    (output / "compile.log").write_text(compiled.stdout + compiled.stderr, encoding="utf-8")
    print(compiled.stdout + compiled.stderr, end="")
    result = None
    if not compiled.returncode:
        result = subprocess.run([str(executable)], cwd=output, text=True, capture_output=True)
        (output / "result.log").write_text(result.stdout + result.stderr, encoding="utf-8")
        print(result.stdout + result.stderr, end="")
    receipt.update({
        "passed": compiled.returncode == 0 and result is not None and result.returncode == 0,
        "compile_exit_code": compiled.returncode,
        "test_exit_code": result.returncode if result is not None else None,
    })
    (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"Receipt: {output / 'receipt.json'}")
    return 0 if receipt["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
