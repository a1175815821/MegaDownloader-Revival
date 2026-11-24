cmake_minimum_required(VERSION 3.20)

project(MegaAccountBridge LANGUAGES C CXX)

set(MEGA_SDK_DIR "" CACHE PATH "MEGA SDK v10.21.1 source directory")
set(VCPKG_ROOT "" CACHE PATH "vcpkg checkout with the SDK's pinned baseline")

if(APPLE)
    set(_md_default_triplet arm64-osx)
elseif(WIN32)
    set(_md_default_triplet x64-windows-static-md)
else()
    set(_md_default_triplet x64-linux)
endif()
set(VCPKG_TARGET_TRIPLET ${_md_default_triplet} CACHE STRING "Bridge dependency architecture")
set(VCPKG_MANIFEST_DIR "${MEGA_SDK_DIR}")

# Windows uses the SDK's Schannel curl backend, so OpenSSL is not needed there.
# macOS and Linux use the SDK's OpenSSL backend.
if(WIN32)
    set(_md_openssl_default OFF)
else()
    set(_md_openssl_default ON)
endif()
set(USE_OPENSSL ${_md_openssl_default} CACHE BOOL "Use the SDK's OpenSSL curl backend")
set(ENABLE_SYNC OFF CACHE BOOL "Do not build cloud sync")
set(USE_FREEIMAGE OFF CACHE BOOL "Do not build thumbnail support")
set(USE_FFMPEG OFF CACHE BOOL "Do not build video processing")
set(USE_PDFIUM OFF CACHE BOOL "Do not build PDF processing")
set(USE_READLINE OFF CACHE BOOL "Do not build the SDK console")
set(ENABLE_ISOLATED_GFX OFF CACHE BOOL "Do not build the graphics worker")

include("${MEGA_SDK_DIR}/cmake/modules/vcpkg_management.cmake")
process_vcpkg_libraries("${MEGA_SDK_DIR}/cmake")

set(BUILD_SHARED_LIBS OFF)
add_subdirectory("${MEGA_SDK_DIR}" "sdk")
include(LimitSdkReceive.cmake)
add_library(MegaAccountBridge SHARED MegaAccountBridge.cpp)
target_link_libraries(MegaAccountBridge PRIVATE MEGA::SDKlib)
target_compile_features(MegaAccountBridge PRIVATE cxx_std_20)

# The managed resolver loads the library from its own directory. Keep any dependent
# SDK-linked libraries resolvable from that same directory in the packaged app.
if(APPLE)
    set_target_properties(MegaAccountBridge PROPERTIES
        INSTALL_RPATH "@loader_path"
        BUILD_RPATH "@loader_path")
elseif(UNIX)
    set_target_properties(MegaAccountBridge PROPERTIES
        INSTALL_RPATH "$ORIGIN"
        BUILD_RPATH "$ORIGIN")
endif()

install(TARGETS MegaAccountBridge RUNTIME DESTINATION . LIBRARY DESTINATION .)
