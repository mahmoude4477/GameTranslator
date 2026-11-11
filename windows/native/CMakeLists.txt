# SPDX-License-Identifier: AGPL-3.0-only
# gametranslator_native.dll: libchiaki + FFmpeg (مفكك h264 و swscale) في DLL واحد بربط ساكن.
# يُبنى على Windows بـ MSYS2 mingw64؛ ويُبنى على Mac أيضًا
# (للكود والاختبارات؛ وهناك تكون المكتبة المشتركة .dylib).
#
#   cmake -S windows/native -B build/native -G Ninja -DFFMPEG_PREFIX=<تثبيت FFmpeg الساكن>
#   cmake --build build/native
cmake_minimum_required(VERSION 3.20)
project(gametranslator_native C)

set(CHIAKI_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../third-party/chiaki-ng" CACHE PATH "مصادر chiaki-ng")
set(FFMPEG_PREFIX "" CACHE PATH "تثبيت FFmpeg (include/, lib/)")
option(GAMETRANSLATOR_NATIVE_TESTS "بناء الاختبارات الأصلية" ON)

if(WIN32)
	# لتُعثر على المكتبات الساكنة فقط: يجب ألا يعتمد الـ DLL على مكتبات DLL لوقت التشغيل في MSYS2.
	set(CMAKE_FIND_LIBRARY_SUFFIXES .a)
	set(OPENSSL_USE_STATIC_LIBS ON CACHE BOOL "" FORCE)
	# وإلا فإن ملفات miniupnpc الرأسية تعلن __declspec(dllimport).
	add_compile_definitions(MINIUPNP_STATICLIB)
endif()

# يُبنى من chiaki ما يلزم فقط: الاتصال والتسجيل وبث الصورة.
foreach(off CHIAKI_ENABLE_GUI CHIAKI_ENABLE_CLI CHIAKI_ENABLE_TESTS CHIAKI_ENABLE_FFMPEG_DECODER
		CHIAKI_ENABLE_PI_DECODER CHIAKI_LIB_ENABLE_OPUS CHIAKI_ENABLE_SPEEX CHIAKI_ENABLE_SETSU
		CHIAKI_ENABLE_STEAMDECK_NATIVE CHIAKI_ENABLE_STEAM_SHORTCUT CHIAKI_ENABLE_BOREALIS
		BUILD_SHARED_LIBS USE_NGHTTP2 USE_LIBIDN2 CURL_ZSTD CURL_BROTLI CURL_USE_LIBPSL CURL_USE_LIBSSH2
		USE_LIBRTMP CURL_ZLIB)
	set(${off} OFF CACHE STRING "" FORCE)
endforeach()
set(CURL_DISABLE_LDAP ON CACHE BOOL "" FORCE)
add_subdirectory("${CHIAKI_SOURCE_DIR}" chiaki EXCLUDE_FROM_ALL)

# FFmpeg: على Windows مكتبات ساكنة مبنية من المصدر (LGPL؛ h264 + swscale فقط).
foreach(lib avcodec swscale avutil)
	find_library(FFMPEG_${lib} NAMES ${lib} HINTS "${FFMPEG_PREFIX}/lib" NO_DEFAULT_PATH)
	if(NOT FFMPEG_${lib})
		find_library(FFMPEG_${lib} NAMES ${lib} REQUIRED)
	endif()
endforeach()
find_path(FFMPEG_INCLUDE_DIR libavcodec/avcodec.h HINTS "${FFMPEG_PREFIX}/include")

add_library(gametranslator_deps INTERFACE)
target_link_libraries(gametranslator_deps INTERFACE chiaki-lib ${FFMPEG_avcodec} ${FFMPEG_swscale} ${FFMPEG_avutil})
target_include_directories(gametranslator_deps INTERFACE "${FFMPEG_INCLUDE_DIR}" "${CMAKE_CURRENT_SOURCE_DIR}")
if(WIN32)
	# اعتماديات Windows لـ OpenSSL الساكن و libevent و curl (Schannel) و FFmpeg؛ وكلها مكتبات DLL للنظام.
	target_link_libraries(gametranslator_deps INTERFACE
		ws2_32 wsock32 iphlpapi bcrypt crypt32 secur32 advapi32 user32 shell32 ole32 gdi32 normaliz)
	target_link_options(gametranslator_deps INTERFACE -static -static-libgcc)
elseif(APPLE)
	target_link_libraries(gametranslator_deps INTERFACE z "-framework CoreFoundation" "-framework Security"
		"-framework SystemConfiguration" "-framework CoreServices")
endif()

add_library(gametranslator_native SHARED gametranslator_native.c gametranslator_native.h)
target_link_libraries(gametranslator_native PRIVATE gametranslator_deps)
set_target_properties(gametranslator_native PROPERTIES PREFIX "" C_VISIBILITY_PRESET hidden)
if(WIN32)
	# تُحذف رموز تصحيح الأخطاء (فيصغر الـ DLL من نحو 10 MB).
	target_link_options(gametranslator_native PRIVATE -Wl,--no-undefined -s)
endif()

if(GAMETRANSLATOR_NATIVE_TESTS)
	# الاختبار يضمّن الملف المصدري مباشرة للوصول إلى الدوال الداخلية.
	add_executable(gametranslator_native_test tests/native_test.c)
	target_link_libraries(gametranslator_native_test PRIVATE gametranslator_deps)
endif()
