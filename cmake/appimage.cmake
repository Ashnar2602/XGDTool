# cmake/appimage.cmake
# Provides a custom CMake target `appimage` that packages the XGDTool GUI
# binary into a self-contained AppImage using linuxdeploy.
#
# Included automatically by the top-level CMakeLists.txt when
# ENABLE_GUI=ON and ENABLE_APPIMAGE=ON (default ON on UNIX/GUI builds).
#
# Usage after a successful cmake --build:
#   cmake --build <build_dir> --target appimage
#
# Output:
#   <build_dir>/XGDTool-GUI-Linux-<arch>.AppImage

# Only meaningful on UNIX GUI builds
if(NOT UNIX OR NOT ENABLE_GUI)
    return()
endif()

option(ENABLE_APPIMAGE "Build an AppImage package for the GUI" ON)
if(NOT ENABLE_APPIMAGE)
    return()
endif()

# Detect target architecture (host or cross-compile target)
if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
    set(APPIMAGE_ARCH "aarch64")
else()
    set(APPIMAGE_ARCH "x86_64")
endif()

set(APPIMAGE_OUTPUT_NAME "XGDTool-GUI-Linux-${APPIMAGE_ARCH}.AppImage")

# Download linuxdeploy if not already present
set(LINUXDEPLOY_BINARY "${CMAKE_BINARY_DIR}/linuxdeploy-${APPIMAGE_ARCH}.AppImage")

if(APPIMAGE_ARCH STREQUAL "aarch64")
    set(LINUXDEPLOY_URL "https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-aarch64.AppImage")
else()
    set(LINUXDEPLOY_URL "https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-x86_64.AppImage")
endif()

# GTK plugin for bundling GTK/wx runtime libraries
if(APPIMAGE_ARCH STREQUAL "x86_64")
    set(LINUXDEPLOY_GTK_PLUGIN_URL "https://github.com/linuxdeploy/linuxdeploy-plugin-gtk/releases/download/continuous/linuxdeploy-plugin-gtk-x86_64.AppImage")
    set(LINUXDEPLOY_GTK_PLUGIN "${CMAKE_BINARY_DIR}/linuxdeploy-plugin-gtk-${APPIMAGE_ARCH}.AppImage")
endif()

# Icon and desktop file paths
set(APPIMAGE_ICON   "${CMAKE_SOURCE_DIR}/resources/app_icon.png")
set(APPIMAGE_DESKTOP "${CMAKE_SOURCE_DIR}/resources/xgdtool.desktop")

# AppDir staging directory
set(APPIMAGE_APPDIR "${CMAKE_BINARY_DIR}/AppDir")

add_custom_target(appimage
    DEPENDS ${PROJECT_NAME}
    COMMENT "Building AppImage for XGDTool GUI (${APPIMAGE_ARCH})..."

    # Download linuxdeploy if needed
    COMMAND ${CMAKE_COMMAND} -E echo "Checking linuxdeploy..."
    COMMAND test -f "${LINUXDEPLOY_BINARY}" || 
            (${CMAKE_COMMAND} -E echo "Downloading linuxdeploy..." &&
             curl -L --retry 3 -o "${LINUXDEPLOY_BINARY}" "${LINUXDEPLOY_URL}" &&
             chmod +x "${LINUXDEPLOY_BINARY}")

    # Download GTK plugin (x86_64 only for now)
    COMMAND ${CMAKE_COMMAND} -E echo "Checking linuxdeploy-plugin-gtk..."
    COMMAND sh -c "if [ '${APPIMAGE_ARCH}' = 'x86_64' ]; then \
                     test -f '${LINUXDEPLOY_GTK_PLUGIN}' || \
                       (curl -L --retry 3 -o '${LINUXDEPLOY_GTK_PLUGIN}' '${LINUXDEPLOY_GTK_PLUGIN_URL}' && \
                        chmod +x '${LINUXDEPLOY_GTK_PLUGIN}'); \
                   fi"

    # Clean and prepare AppDir
    COMMAND ${CMAKE_COMMAND} -E remove_directory "${APPIMAGE_APPDIR}"
    COMMAND ${CMAKE_COMMAND} -E make_directory   "${APPIMAGE_APPDIR}/usr/bin"
    COMMAND ${CMAKE_COMMAND} -E make_directory   "${APPIMAGE_APPDIR}/usr/share/applications"
    COMMAND ${CMAKE_COMMAND} -E make_directory   "${APPIMAGE_APPDIR}/usr/share/pixmaps"

    # Stage the executable and languages directory
    COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${PROJECT_NAME}>"
            "${APPIMAGE_APPDIR}/usr/bin/XGDTool"
    COMMAND ${CMAKE_COMMAND} -E copy_directory
            "${CMAKE_SOURCE_DIR}/languages"
            "${APPIMAGE_APPDIR}/usr/bin/languages"

    # Stage icon and .desktop file
    COMMAND ${CMAKE_COMMAND} -E copy "${APPIMAGE_ICON}"   "${APPIMAGE_APPDIR}/usr/share/pixmaps/xgdtool.png"
    COMMAND ${CMAKE_COMMAND} -E copy "${APPIMAGE_DESKTOP}" "${APPIMAGE_APPDIR}/usr/share/applications/xgdtool.desktop"

    # Run linuxdeploy with GTK plugin to bundle shared libraries
    COMMAND sh -c "export OUTPUT='${CMAKE_BINARY_DIR}/${APPIMAGE_OUTPUT_NAME}'; \
                   export ARCH='${APPIMAGE_ARCH}'; \
                   if [ '${APPIMAGE_ARCH}' = 'x86_64' ] && [ -f '${LINUXDEPLOY_GTK_PLUGIN}' ]; then \
                     DEPLOY_PLUGIN='--plugin gtk'; \
                     export LINUXDEPLOY_PLUGIN_GTK='${LINUXDEPLOY_GTK_PLUGIN}'; \
                   else \
                     DEPLOY_PLUGIN=''; \
                   fi; \
                   '${LINUXDEPLOY_BINARY}' \
                     --appdir '${APPIMAGE_APPDIR}' \
                     --executable '${APPIMAGE_APPDIR}/usr/bin/XGDTool' \
                     --icon-file '${APPIMAGE_ICON}' \
                     --desktop-file '${APPIMAGE_DESKTOP}' \
                     $DEPLOY_PLUGIN \
                     --output appimage"

    COMMAND ${CMAKE_COMMAND} -E echo "AppImage created: ${CMAKE_BINARY_DIR}/${APPIMAGE_OUTPUT_NAME}"
    WORKING_DIRECTORY "${CMAKE_BINARY_DIR}"
    VERBATIM
)
