# SDK 10.21.1 rejects a whole curl block when it exceeds five seconds of budget.
# At low shares, pace complete blocks instead. Build a copy so the SDK checkout
# remains usable by other builds.
set(curl_source "${MEGA_SDK_DIR}/src/posix/net.cpp")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${curl_source}")
file(READ "${curl_source}" curl_source_text)
string(REPLACE "\r\n" "\n" curl_source_text "${curl_source_text}")
set(receive_check [=[if ((httpio->downloadSpeed +
                     ((httpio->partialdata[GET] + static_cast<m_off_t>(len)) /
                      static_cast<m_off_t>(
                          SpeedController::SPEED_MEAN_CIRCULAR_BUFFER_SIZE_SECONDS))) >
                    httpio->maxspeed[GET])]=])
set(paced_receive [=[const auto limit = httpio->maxspeed[GET];
                const auto window = SpeedController::SPEED_MEAN_CIRCULAR_BUFFER_SIZE_SECONDS;
                bool pause;
                if (limit < CURL_MAX_WRITE_SIZE / window)
                {
                    // Each MegaApi has its own worker thread and curl receive budget.
                    using Clock = std::chrono::steady_clock;
                    static thread_local Clock::time_point lastWrite;
                    static thread_local size_t lastSize = 0;
                    const auto now = Clock::now();
                    const auto delay = std::chrono::microseconds((lastSize * 1000000LL + limit - 1) / limit);
                    pause = now - lastWrite < delay;
                    if (!pause)
                    {
                        lastWrite = now;
                        lastSize = len;
                    }
                    else
                    {
                        // A block is available and intentionally held for the rate limit.
                        httpio->lastdata = req->lastdata = Waiter::ds;
                    }
                }
                else
                {
                    pause = httpio->downloadSpeed + (httpio->partialdata[GET] + static_cast<m_off_t>(len)) / window > limit;
                }
                if (pause)]=])
string(FIND "${curl_source_text}" "${receive_check}" receive_check_offset)
if(receive_check_offset EQUAL -1)
    message(FATAL_ERROR "The MEGA SDK curl receiver differs from the pinned 10.21.1 source.")
endif()
string(REPLACE "${receive_check}" "${paced_receive}" curl_source_text "${curl_source_text}")
set(paced_source "${CMAKE_CURRENT_BINARY_DIR}/MegaAccountCurlNet.cpp")
file(CONFIGURE OUTPUT "${paced_source}" CONTENT "${curl_source_text}" @ONLY)
set_property(SOURCE "${MEGA_SDK_DIR}/src/win32/net.cpp" TARGET_DIRECTORY SDKlib PROPERTY HEADER_FILE_ONLY TRUE)
target_sources(SDKlib PRIVATE "${paced_source}")
