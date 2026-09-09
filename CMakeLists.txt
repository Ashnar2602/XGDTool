cmake_minimum_required(VERSION 3.11)

if (WIN32)
    set(VCPKG_DIR ${CMAKE_CURRENT_SOURCE_DIR}/external/vcpkg)
    include (${CMAKE_SOURCE_DIR}/cmake/setup_vcpkg.cmake)
    set(CMAKE_TOOLCHAIN_FILE ${VCPKG_DIR}/scripts/buildsystems/vcpkg.cmake CACHE STRING "Vcpkg toolchain file")
    set(VCPKG_TARGET_TRIPLET x64-windows-static CACHE STRING "Vcpkg target triplet")
endif()

project(XGDTool)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED True)

if(DEFINED XGDTOOL_BUILD_GUI)
    set(ENABLE_GUI ${XGDTOOL_BUILD_GUI})
endif()
option(ENABLE_GUI "Enable GUI" ON)  # Set to OFF for CLI only

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Choose the type of build (Debug or Release)" FORCE)
endif()

message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

if(MSVC)
    set(CMAKE_CXX_FLAGS_DEBUG "/Zi /Od /MTd")
    set(CMAKE_CXX_FLAGS_RELEASE "/O2 /GL /MT")
    set(CMAKE_EXE_LINKER_FLAGS_RELEASE "/LTCG /INCREMENTAL:NO")
    set(CMAKE_SHARED_LINKER_FLAGS_RELEASE "/LTCG /INCREMENTAL:NO")
    set(CMAKE_MODULE_LINKER_FLAGS_RELEASE "/LTCG /INCREMENTAL:NO")
elseif(CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
    set(CMAKE_CXX_FLAGS_DEBUG "-g -O0")
    set(CMAKE_CXX_FLAGS_RELEASE "-O2 -flto")
    set(CMAKE_EXE_LINKER_FLAGS_RELEASE "-flto")
    set(CMAKE_SHARED_LINKER_FLAGS_RELEASE "-flto")
    set(CMAKE_MODULE_LINKER_FLAGS_RELEASE "-flto")
elseif(UNIX)
    set(CMAKE_CXX_FLAGS_RELEASE "-O2")
endif()


include(${CMAKE_SOURCE_DIR}/cmake/embed_resources.cmake)

# Apply multithread patch to ZArchive submodule if needed
if(EXISTS "${CMAKE_SOURCE_DIR}/external/ZArchive/include/zarchive/zarchivewriter.h")
    file(READ "${CMAKE_SOURCE_DIR}/external/ZArchive/include/zarchive/zarchivewriter.h" ZARCHIVE_HEADER_CONTENT)
    if(NOT ZARCHIVE_HEADER_CONTENT MATCHES "numThreads")
        message(STATUS "Applying multithread patch to ZArchive...")
        find_package(Git QUIET)
        if(GIT_FOUND)
            execute_process(
                COMMAND ${GIT_EXECUTABLE} apply --ignore-space-change --ignore-whitespace --whitespace=nowarn "${CMAKE_SOURCE_DIR}/cmake/zarchive_multithread.patch"
                WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}/external/ZArchive"
                RESULT_VARIABLE PATCH_RES
            )
        endif()
    endif()
endif()

add_subdirectory(${CMAKE_SOURCE_DIR}/external/ZArchive)

set(SRC_DIR ${CMAKE_SOURCE_DIR}/src)

include_directories(
    ${SRC_DIR} 
)

set(SRC 
    ${SRC_DIR}/main.cpp
    
    ${SRC_DIR}/XGDLog.cpp
    ${SRC_DIR}/XGDException.cpp

    ${SRC_DIR}/InputHelper/InputHelper.cpp
    ${SRC_DIR}/InputHelper/InputHelper_FS.cpp

    ${SRC_DIR}/ImageReader/ImageReader.cpp
    ${SRC_DIR}/ImageReader/XisoReader/XisoReader.cpp
    ${SRC_DIR}/ImageReader/GoDReader/GoDReader.cpp
    ${SRC_DIR}/ImageReader/CCIReader/CCIReader.cpp
    ${SRC_DIR}/ImageReader/CSOReader/CSOReader.cpp

    ${SRC_DIR}/ImageWriter/ImageWriter.cpp
    ${SRC_DIR}/ImageWriter/XisoWriter/XisoWriter.cpp
    ${SRC_DIR}/ImageWriter/GoDWriter/GoDWriter.cpp
    ${SRC_DIR}/ImageWriter/CCIWriter/CCIWriter.cpp
    ${SRC_DIR}/ImageWriter/ZARWriter/ZARWriter.cpp
    ${SRC_DIR}/ImageWriter/CSOWriter/CSOWriter.cpp

    ${SRC_DIR}/ImageExtractor/ImageExtractor.cpp

    ${SRC_DIR}/ZARExtractor/ZARExtractor.cpp

    ${SRC_DIR}/Executable/ExeTool.cpp
    ${SRC_DIR}/Executable/AttachXbeTool.cpp

    ${SRC_DIR}/TitleHelper/TitleHelper.cpp

    ${SRC_DIR}/AvlTree/AvlTree.cpp
    ${SRC_DIR}/AvlTree/AvlTree_Calculate.cpp
    ${SRC_DIR}/AvlTree/AvlTree_Traverse.cpp
    ${SRC_DIR}/AvlTree/AvlTree_Create.cpp
    ${SRC_DIR}/AvlTree/AvlIterator.cpp

    ${SRC_DIR}/SplitFStream/SplitIFStream.cpp
    ${SRC_DIR}/SplitFStream/SplitOFStream.cpp

    ${SRC_DIR}/Utils/EndianUtils.cpp
    ${SRC_DIR}/Utils/StringUtils.cpp
    ${SRC_DIR}/Utils/LocalizationManager.cpp

    ${SRC_DIR}/Formats/Xiso.cpp
)

if (ENABLE_GUI)

    list(APPEND SRC
        ${SRC_DIR}/GUI/MainFrame.cpp
        ${SRC_DIR}/GUI/MainFrame_Create.cpp
        ${SRC_DIR}/GUI/MainFrame_Log.cpp
        ${SRC_DIR}/GUI/CompletionDialog.cpp
    )

    add_definitions(-DENABLE_GUI)

    if (WIN32)
        # Windows GUI: needs .rc for the embedded icon resource
        add_executable(${PROJECT_NAME} WIN32 ${SRC} resources/resource.rc)
    elseif(UNIX)
        # Linux GUI: .rc files are Windows-only; the icon is loaded at runtime from a PNG
        add_executable(${PROJECT_NAME} ${SRC})
    endif()

else()

    if (WIN32)
        add_executable(${PROJECT_NAME} ${SRC} resources/resource.rc)
    else()
        add_executable(${PROJECT_NAME} ${SRC})
    endif()

endif()

if (WIN32)

    find_package(lz4 CONFIG REQUIRED)
    find_package(zstd CONFIG REQUIRED)
    find_package(nlohmann_json CONFIG REQUIRED)
    find_package(CLI11 CONFIG REQUIRED)
    find_package(OpenSSL CONFIG REQUIRED)
    find_package(CURL REQUIRED)
    find_package(wxWidgets CONFIG REQUIRED)

    target_link_libraries(${PROJECT_NAME} PRIVATE
        zarchive

        wx::core 
        wx::base
        wx::xml

        lz4::lz4
        zstd::libzstd
        CLI11::CLI11
        nlohmann_json::nlohmann_json
        OpenSSL::SSL 
        OpenSSL::Crypto
        CURL::libcurl
    )

    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_CURRENT_SOURCE_DIR}/languages
        $<TARGET_FILE_DIR:${PROJECT_NAME}>/languages
    )

    if(MSVC)
        target_compile_options(zarchive PRIVATE /wd4244)
    endif()

elseif(UNIX)

    set(JSON_HPP_URL "https://github.com/nlohmann/json/releases/download/v3.11.2/json.hpp")
    set(JSON_DIR "${CMAKE_SOURCE_DIR}/external/nlohmann")
    set(JSON_HPP_PATH "${JSON_DIR}/json.hpp")

    set(CLI11_URL "https://github.com/CLIUtils/CLI11/releases/download/v2.4.2/CLI11.hpp")
    set(CLI11_DIR "${CMAKE_SOURCE_DIR}/external/CLI")
    set(CLI11_HEADER "${CLI11_DIR}/CLI.hpp")

    file(MAKE_DIRECTORY ${JSON_DIR})
    file(MAKE_DIRECTORY ${CLI11_DIR})

    if(NOT EXISTS ${JSON_HPP_PATH})
        message(STATUS "Downloading json.hpp")
        file(DOWNLOAD ${JSON_HPP_URL} ${JSON_HPP_PATH})
    endif()

    if(NOT EXISTS ${CLI11_HEADER})
        message(STATUS "Downloading CLI11.hpp")
        file(DOWNLOAD ${CLI11_URL} ${CLI11_HEADER})
    endif()

    find_package(PkgConfig REQUIRED)
    set(ENV{PKG_CONFIG_PATH} "/usr/local/lib/pkgconfig:$ENV{PKG_CONFIG_PATH}")

    pkg_check_modules(LZ4 REQUIRED liblz4)
    pkg_check_modules(ZSTD REQUIRED libzstd)
    find_package(OpenSSL REQUIRED)
    find_package(CURL REQUIRED)

    # Option for static linking of application libraries (for portable/AppImage builds)
    option(LINUX_STATIC_LIBS "Link lz4/zstd/openssl/curl statically on Linux" OFF)

    include_directories(
        ${CMAKE_SOURCE_DIR}/external
        ${LZ4_INCLUDE_DIRS}
        ${ZSTD_INCLUDE_DIRS}
        ${OPENSSL_INCLUDE_DIR}
        ${CURL_INCLUDE_DIRS}
    )

    link_directories(
        ${LZ4_LIBRARY_DIRS}
        ${ZSTD_LIBRARY_DIRS}
    )

    # Base libraries shared by both CLI and GUI on Linux
    if(LINUX_STATIC_LIBS)
        set(LINUX_APP_LIBS
            zarchive
            ${LZ4_STATIC_LIBRARIES}
            ${ZSTD_STATIC_LIBRARIES}
            OpenSSL::SSL
            OpenSSL::Crypto
            CURL::libcurl
        )
    else()
        set(LINUX_APP_LIBS
            zarchive
            ${LZ4_LIBRARIES}
            ${ZSTD_LIBRARIES}
            ${OPENSSL_LIBRARIES}
            ${CURL_LIBRARIES}
        )
    endif()

    if (ENABLE_GUI)
        # wxWidgets is only required when building the GUI target
        execute_process(
            COMMAND wx-config --cxxflags
            OUTPUT_VARIABLE wxWidgets_CXX_FLAGS_RAW
            OUTPUT_STRIP_TRAILING_WHITESPACE
            RESULT_VARIABLE wx_config_result
        )
        if(NOT wx_config_result EQUAL 0)
            message(FATAL_ERROR "wx-config not found. Install libwxgtk3.2-dev (or libwxgtk3.0-gtk3-dev) to build the GUI target, or configure with -DENABLE_GUI=OFF for CLI only.")
        endif()

        execute_process(
            COMMAND wx-config --libs
            OUTPUT_VARIABLE wxWidgets_LIBS_RAW
            OUTPUT_STRIP_TRAILING_WHITESPACE
        )

        # Parse wx-config output into a CMake list so -D/-I/-pthread are handled correctly
        separate_arguments(wxWidgets_CXX_FLAGS_LIST UNIX_COMMAND "${wxWidgets_CXX_FLAGS_RAW}")
        separate_arguments(wxWidgets_LIBS_LIST UNIX_COMMAND "${wxWidgets_LIBS_RAW}")

        # GTK3 headers are required on Linux; wx-config --cxxflags omits them.
        # GTK3 includes must be listed BEFORE wx includes because wx/gtk/*.h
        # use GdkWindow/GtkWidget types without self-including gdk/gdk.h.
        pkg_check_modules(GTK3 REQUIRED gtk+-3.0)

        # Apply all wx compile flags (includes -D__WXGTK__, -DWXUSINGDLL, -pthread, etc.)
        # Use target_compile_options so they only apply to the GUI target
        target_compile_options(${PROJECT_NAME} PRIVATE
            ${wxWidgets_CXX_FLAGS_LIST}
            ${GTK3_CFLAGS_OTHER}
            -Wno-ambiguous-reversed-operator
        )

        # GTK3 includes must come BEFORE wx includes in the compile command.
        # target_include_directories preserves insertion order in the link.txt.
        target_include_directories(${PROJECT_NAME} SYSTEM PRIVATE
            ${GTK3_INCLUDE_DIRS}
        )
        # wx includes come after GTK3 (use separate call to ensure ordering)
        target_include_directories(${PROJECT_NAME} SYSTEM PRIVATE
            ${wxWidgets_INCLUDE_DIRS}
        )

        target_link_libraries(${PROJECT_NAME} PRIVATE
            ${LINUX_APP_LIBS}
            ${wxWidgets_LIBS_LIST}
        )
    else()
        # CLI build: no wxWidgets dependency required
        target_link_libraries(${PROJECT_NAME} PRIVATE
            ${LINUX_APP_LIBS}
        )
    endif()

    # Copy languages directory next to the executable after build (mirrors Windows behaviour)
    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_CURRENT_SOURCE_DIR}/languages
        $<TARGET_FILE_DIR:${PROJECT_NAME}>/languages
    )

    # Copy the PNG application icon next to the executable (used by the GUI at runtime)
    if(ENABLE_GUI)
        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
            ${CMAKE_CURRENT_SOURCE_DIR}/resources/app_icon.png
            $<TARGET_FILE_DIR:${PROJECT_NAME}>/app_icon.png
        )
    endif()

else()

    message(FATAL_ERROR "Unsupported system: ${CMAKE_SYSTEM_NAME}")

endif()

# AppImage packaging target (Linux GUI only, opt-in via ENABLE_APPIMAGE)
if(UNIX AND ENABLE_GUI)
    include(${CMAKE_SOURCE_DIR}/cmake/appimage.cmake)
endif()
