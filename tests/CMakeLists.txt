cmake_minimum_required(VERSION 3.16)
project(XGDToolDirectoryRegression LANGUAGES CXX)

enable_testing()
find_package(Python3 REQUIRED COMPONENTS Interpreter)
get_filename_component(XGDTOOL_SOURCE_ROOT "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)
set(FIXTURE_DIR "${CMAKE_CURRENT_BINARY_DIR}/generated")
set(FIXTURE_CPP "${FIXTURE_DIR}/directory_entries_test.cpp")

# Build only the actual enumeration methods and synthetic byte adapter. This
# standalone project intentionally does not configure the full application.
add_custom_command(
    OUTPUT "${FIXTURE_CPP}"
    COMMAND "${Python3_EXECUTABLE}" "${CMAKE_CURRENT_LIST_DIR}/test_directory_entries.py"
        --source-root "${XGDTOOL_SOURCE_ROOT}" --output-dir "${FIXTURE_DIR}" --emit-only
    DEPENDS
        "${CMAKE_CURRENT_LIST_DIR}/test_directory_entries.py"
        "${XGDTOOL_SOURCE_ROOT}/src/ImageReader/ImageReader.cpp"
        "${XGDTOOL_SOURCE_ROOT}/src/ImageReader/ImageReader.h"
        "${XGDTOOL_SOURCE_ROOT}/src/Utils/StringUtils.cpp"
        "${XGDTOOL_SOURCE_ROOT}/src/Utils/StringUtils.h"
        "${XGDTOOL_SOURCE_ROOT}/src/Formats/Xiso.h"
    VERBATIM)

add_executable(directory_entries_regression "${FIXTURE_CPP}")
target_include_directories(directory_entries_regression PRIVATE "${XGDTOOL_SOURCE_ROOT}/src")
target_compile_features(directory_entries_regression PRIVATE cxx_std_20)
set_target_properties(directory_entries_regression PROPERTIES CXX_EXTENSIONS OFF)
if(MSVC)
    target_compile_options(directory_entries_regression PRIVATE /W4 /WX /EHsc)
else()
    target_compile_options(directory_entries_regression PRIVATE -Wall -Wextra -Werror)
endif()

set(DIRECTORY_CASES
    valid-left-00FF-retains-entire-subtree-and-ignores-FFFF
    ordinary-leaf-control
    FFFF-root-padding-ignored
    nested-tree-preserves-directory-and-relative-paths
    executable-only-reaches-valid-00FF-subtree
    total-bytes-and-entry-cache-retain-all-files
    nonzero-partition-offset)
foreach(CASE_NAME IN LISTS DIRECTORY_CASES)
    add_test(NAME "directory-${CASE_NAME}" COMMAND directory_entries_regression "${CASE_NAME}")
endforeach()
