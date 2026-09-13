# 변경 로그

본 프로젝트의 모든 중요한 변경 사항은 이 문서에 기록됩니다. 형식은 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)를 따르며, 버전 번호는 [시맨틱 버저닝](https://semver.org/lang/zh-CN/)을 따릅니다.

*사용자 대상 버전 하이라이트 요약은 [GitHub Releases](../../../releases)를 참조하십시오.*

---

## 버전 하이라이트 요약

| 버전 | 날짜 | 주제 |
| --- | --- | --- |
| 2.5.0 | 2026-09-14 | 정식 버전: 온라인 시청 동시성 수정 + 버전 번호 정식화 및 업데이트 채널 진입 + 4개 언어 문서 보완 |
| 2.5 RC2 | 2026-09-13 | 프로덕션 감사 3차 수정: 릴리스 차단 / 안정성 / 경험 및 심층 방어 |
| 2.5 RC1 | 2026-09-12 | MEGA 할당량 서킷 브레이커 + 카운트다운 배너; 실패 자동 복구 기본 활성화 |
| 2.4.7 | 2026-09-12 | 업데이트 알림 3개 옵션; 폐기된 검색 엔진 통합 제거 |
| 2.4.6 | 2026-09-04 | 가짜 성공 / 조용한 실패 특화 수정 |
| 2.4.5 | 2026-09-02 | 전면 코드 리뷰 후 시스템 수정(16건) |
| 2.4.4 | 2026-09-01 | 하위 폴더 링크 다운로드; MetaMAC 청크 초기값 수정; 9건 보안 강화 |
| 2.4.3 | 2026-08-26 | 7z 압축 해제; Web LAN 푸시; 클립보드 누락 감지 수정 |
| 2.4.2 | 2026-08-19 | 다운로드 파일 실제 손상 수정(MetaMAC / 이어받기 정렬) |
| 2.4.1 | 2026-08-15 | 다운로드 완료 후 오류 표시 수정 |
| 2.4.0 | 2026-08-14 | 21건 버그 수정(보안 / 누수 / 동시성 / 데드 코드) |
| 2.3.0 | 2026-08-13 | 암호화 실패 충돌, 리소스 누수, `Thread.Abort` 제거 |
| 2.2.x | 2026-07~08 | 경로 보안, 다운로드 무결성, 원자적 설정 저장, Web CSRF |
| 2.1.0 | 2026-07-19 | 다크 테마 사용성 수정 |
| 2.0.0 | 2026-07-13 | 4단계 60여 건 수정; 다크/라이트 테마; 코드 정리 |
| 1.9.x | 2026-07-05 | 신버전 MEGA 링크 형식 인식 수정 |
| 1.8.0 | 원본 | 디컴파일 소스, 부활 계획의 출발점 |

---

## v2.4 시리즈 상세 변경

다음은 v2.4 각 버전의 변경 내역입니다(같은 버전은 위 버전 기록에도 항목이 있습니다).

### 변경 내역

#### 업데이트 알림 및 검색 엔진 정리(v2.4.7)

| 변경                   | 설명                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------- |
| 업데이트 알림 3개 옵션             | 예=지금 업데이트; 아니요=3시간 후 다시 알림; 취소=현재 버전 다시 알리지 않음(`UpdateSkipVersion`은 버전별로 기록되며, 새 버전 출시 후 자동으로 알림 복원) |
| 검색 엔진 통합 제거           | "찾기" 메뉴의 4개 도메인(megafiles.me/megafindr/megasearch.co 등)이 모두 서비스 종료됨; `mega://mega-search?` 링크 파싱도 함께 제거 |

#### 가짜 성공/조용한 실패 수정(v2.4.6)

| 수정                     | 설명                                                                                             |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| 재시작 후 가짜 성공(P1)          | `Verificando`/`Descomprimiendo`가 `Completado`로 표시되었으나 두 상태 모두 1바이트도 디스크에 기록되지 않았을 수 있음; 모두 `EnCola`로 되돌려 이어받기로 계속 진행          |
| 설정 저장 가짜 성공           | `GuardarXML` 실패 시 하위 로그에만 기록하고 UI는 그대로 성공 팝업 표시; 이제 `ErrorConfig` 실패를 확인하면 오류 팝업을 표시하고 창에 머무름                                  |
| 대문자 링크 조용히 버려짐          | IgnoreCase로 매칭하고 대소문자 구분 방식으로 검증하여 `HTTPS://MEGA.NZ/...`가 무반응으로 버려짐; 6곳의 정규식 + 접두사 비교를 모두 대소문자 무시 방식으로 보완              |
| 비정상 enc 충돌            | base64url 길이가 %4==1이면 `ArgumentOutOfRangeException`이 그대로 충돌 발생; 사전에 판정하여 친화적인 오류 팝업 표시                                    |
| 폴더 API 빈 응답 NRE     | 비정상 응답(빈 문자열/프록시 HTML)에서 NRE 발생; Try/Catch + 빈 값 검사를 추가하고 일괄적으로 "잘못된 서버 응답" 보고                                    |
| ELC 하위 범위 유지           | `MegaLink`에 하위 범위 필드 추가, 인코딩 시 `/folder/하위ID` 또는 `/file/파일ID` 접미사 추가; 구버전 디코더는 접미사를 무시=기존 동작, 신버전은 하위 범위 복원   |
| 언어 키 누락                | en-US/zh-CN 각각 +5(ELC 성공 알림, URL 필수, VLC 경로가 잘못됨, ELC 열기 메뉴, 설정 저장 실패)                             |

#### 안정성 및 보안(v2.4.5)

| 수정             | 설명                                                                                       |
| -------------- | ------------------------------------------------------------------------------------------ |
| 전역 예외 보호       | 이전에는 아무런 보호가 없어 UI 예외가 발생하면 바로 비정상 종료됨; 이제 로그를 기록하고 종료하지 않으며, 백그라운드 스레드 예외도 로그를 남김                           |
| 목록 새로고침 충돌       | 4개의 AspectGetter가 오류 발생 시 매 행 다시 그릴 때마다 창을 띄운 뒤 충돌; 로그 기록 후 플레이스홀더 값 반환으로 변경                              |
| 볼륨 누락 RAR 가짜 성공   | `IsComplete=False`인데 조용히 건너뛰고 상위에 "압축 해제 성공" 보고; 이제 명시적으로 오류 발생                                       |
| UI 멈춤         | 청크 실패 백오프의 바쁜 대기가 UI 스레드에서 실행됨(최대 16.5초); 스레드 풀로 이동                                          |
| Streaming Range | RFC 7233 준수: 접미사/개방 구간, 416 응답, `bytes=0-0`이 더 이상 전체 파일을 가져오지 않음, 응답 본문이 Content-Length를 초과하여 보내지 않음        |
| 스트리밍 라이브러리 CSRF     | Delete/Save/OpenVLC/Import/Export에 POST + 토큰 요구(Web 화면의 EnsureCsrf 패턴 재사용)              |
| 로그인 속도 제한          | 동시 실행 상한 4 + 60초 윈도우 실패 잠금 10회, PBKDF2 POST 폭주로 스레드 풀이 가득 차는 것 방지                                |
| Stegano 디스크 기록 보안   | 메모리 인코딩+검증 통과 후에만 디스크에 기록(더 이상 손상된 .jpg를 남기지 않음); `WriteAllBytes`로 잘라 덮어쓰기(더 이상 이전 파일 꼬리에 이어 붙이지 않음)             |
| 리소스 누수          | FileDownloader 핸들, `CreateDecryptor`/MD5 Using, 5곳의 Mutex Try/Finally, 5곳의 호버 ToolTip       |
| 유지보수성           | vbproj 데드 참조, DPI 설정 PerMonitorV2로 통일, 하드코딩된 영어 메시지를 언어 시스템에 연결(en-US/zh-CN 항목 신규)          |

#### 신기능 및 무결성(v2.4.4)

| 기능/수정          | 설명                                                                                                                                                      |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 하위 폴더 링크 다운로드       | `mega.nz/folder/루트ID#키/folder/하위ID`는 지정된 하위 폴더만 다운로드(경로 기준 재지정); `/file/파일ID`는 지정된 단일 파일만 다운로드——이전에는 모두 루트 폴더 전체를 다운로드했음                                                                  |
| MetaMAC 청크 초기값 수정 | 청크 CBC-MAC 초기값을 0 IV에서 파일 nonce를 두 번 복사한 `[n0,n1,n0,n1]`로 변경(SDK `SymmCipher::ctr_crypt`에 맞춤), 8 words key 다운로드 완료 후 반드시 오보하던 체크섬 오류 수정                                         |
| MetaMAC 표준 체크섬   | "청크 경계 접두사 일치" 관용 로직 제거, SDK와 동일하게 변경: 전체 파일을 다 읽은 뒤 한 번에 전체 비교                                                                                                                |
| 9건 보안 강화        | StripNullCharacters 오프셋 수정; AES 실패 시 Nothing 반환 및 영속화 지점에서 이전 값 유지; 암호문에 무작위 IV 형식 신규(기존 데이터와 호환); Web 암호 PBKDF2(100k)+무작위 솔트; Streaming 고정 시간 암호 비교; PSK 비 ASCII 검증; ClientConnected 리플렉션 견고화 |

#### 신기능(v2.4.3)

| 기능        | 설명                                                                          |
| --------- | --------------------------------------------------------------------------- |
| 7z 압축 해제     | 시스템 7-Zip 우선 사용, 미설치 시 내장 7zr.exe(퍼블릭 도메인) 자동 해제; 암호 및 multipart 분할 압축 지원; 압축 해제 전 PathGuard 검증으로 경로 탈출 방지 |
| Web LAN 푸시 | "LAN 액세스 허용" 스위치(기본값 꺼짐), 켜면 휴대폰/LAN 장치가 브라우저를 통해 다운로드 푸시 가능; 암호 보호 강제; 사용자 지정 바인딩 IP 지원(비워 두기=모든 네트워크 어댑터)            |
| 클립보드 모니터링 수정   | 브라우저 지연 렌더링 + 클립보드 점유 경쟁으로 웹페이지 복사가 누락됨——재시도 읽기로 변경, 모든 액세스에 예외 보호 추가                                 |

#### 다운로드 무결성(v2.4.2)

| 수정          | 설명                                                                     |
| ----------- | ---------------------------------------------------------------------- |
| MetaMAC 알고리즘  | 청크 스케줄을 MEGA SDK `ChunkedHash`에 맞춤: 128 KiB × i(i=1..8) 후 1 MiB 고정; 빈 파일은 (0,0) 반환 |
| 다운로드 완료 판정      | "파일 크기 일치 시 강제 완료" 제거; 실제 chunk가 모두 완료되어야만 완료로 판정; 120초 시간 초과 시 실패 보고 및 이어받기 지점 유지                   |
| CTR 키 스트림 어긋남 방지 | 중단 flush와 이어받기 시작점을 16바이트 정렬로 강제; 시작 시 구버전에 남은 비정렬 진행률을 되돌림——"크기는 맞지만 내용이 손상됨" 근절                |

#### 안정성(v2.4.0/2.4.1)

| 수정       | 설명                                                |
| -------- | ------------------------------------------------- |
| 백그라운드 스레드 팝업 멈춤 | 다운로드 실패를 UI 스레드를 통해 표시하도록 변경; 종료 중 크로스 스레드 MsgBox에 `IsDisposed` 보호 추가   |
| 동시성 오염     | Streaming 모듈 AJAX 응답을 `AsyncLocal`로 변경, 다중 요청이 서로 간섭하지 않음        |
| 리소스 누수     | Mutex `Try/Finally` 해제; `BackgroundWorker.Dispose` |
| 공개 링크 오보   | 4 words key에 MetaMAC이 없을 때 검증을 건너뜀(로그 기록), 더 이상 실패로 오판하지 않음           |

---

## [2.5.0] - 2026-09-14

정식 버전. RC2 대비 증분은 매우 작습니다: 동시성 버그 1건 수정, 버전 번호 정식화 및 업데이트 채널 진입, 문서 4개 언어 보완. 핵심 주제: **RC 검증 통과, 그대로 정식화**.

### 🐛 수정: 온라인 시청 중복 클릭 동시 파싱

([AddLinks.vb](../Forms/AddLinks.vb))"온라인 시청" 버튼을 연타/더블 클릭하면 `ResolveUrlsAsync`가 동시에 두 번 실행되어 같은 링크 묶음이 두 번 파싱되며, 심한 경우 VLC가 두 개 실행됩니다. 이제 `_watchResolving` 진행 중 플래그 + 파싱 중 버튼 비활성화를 추가하고, 콜백의 `Finally`에서 복원합니다(이전에는 아무런 보호가 없었음).

### 📦 버전 번호 정식화, 업데이트 채널 진입

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5`(제목 표시줄/정보/로그의 표시 버전에서 더 이상 RC 접미사가 붙지 않음)
- `VERSION_UPDATE`는 `2.5`로 유지(의도적으로 변경하지 않음: `Main.CheckVersionStatistics`가 `Double`로 파싱하므로 `2.5.0`은 파싱에 실패하여 새 버전 통계 ping이 유실됨)
- `docs/version.xml` → `2.5.0.0`: 2.4.7 및 그 이전 버전은 이 시점부터 업데이트 알림을 받게 됨; 2.5.0은 원격과 동일하므로 더 이상 알리지 않음

### 🌐 문서: 4개 언어 전체 보완(수동 유지)

- `docs/README.zh-TW.md / docs/README.ko-KR.md`, `docs/CHANGELOG.{zh-TW,ja-JP,ko-KR}.md`, `docs/CONTRIBUTING.{zh-TW,ja-JP,ko-KR}.md` 신규 추가, 영문 CHANGELOG를 실제 영어로 다시 작성
- 자동 번역 파이프라인 전체 폐기(`i18n/` 스크립트 + Docs i18n 워크플로 삭제), 이후 모든 언어 버전은 수동으로 동기화하며, 한 곳을 수정하면 다른 언어도 함께 동기화할 것

---

## [2.5 RC2] - 2026-09-13

프로덕션 수준의 파괴적 감사 이후 3차 수정(RC1 이후의 새 변경은 모두 여기에 있음). 핵심 주제: **조용한 실패와 고빈도 멈춤 현상 제거**.

### 🐛 RC2 수정: 릴리스 차단 5건(Batch-1)

([Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb))

- Web 푸시와 수동 링크 추가를 같은 경로로 통일: `ControlRemotoAgregarLinks`가 먼저 `URLProcessor.ProcessURLs`를 거쳐 폴더/ELC를 전개(이전에는 폴더 체인이 단일 손상 작업이 되었음); `pckname`은 `PathGuard`로 정화(이전에는 문자열 이어 붙이기였으며, 인증된 임의 디렉터리 생성+디스크 탈출 가능); 하위 디렉터리 구조 유지
- 폴더 건너뜀 카운트: 단일 노드 복호화 실패를 집계, 일부 건너뜀은 Warning으로 기록(예시 handle 포함, key 자료는 기록하지 않음), 전부 실패 시 오류 발생(이전에는 파일이 0개여도 성공 보고)
- 0바이트 빈 파일 단락 처리: 활성 확인 후에도 0이면 바로 빈 파일을 기록하고 MetaMAC을 검증(기대값 `(0,0)`), 정상 이름 바꾸기와 성공 이벤트 경로로 진행(이전에는 `GetDataPart` 오류 + 자동 복구 헛돌기); 이름 바꾸기 충돌 로직을 `RenamePartToReal`로 추출하여 공용 사용
- 검증 취소 플래그: `bgArranque`는 `CancelAsync` 불가이므로 `_StartupCancelled` 플래그 추가, Stop/Dispose 시 플래그 설정 후 검증 완료 시 다운로더를 더 이상 생성하지 않음(이전에는 Stop 후 반드시 부활했음)
- 손상된 ELC per-URL 격리: 단일 항목 실패 시 해당 항목만 건너뛰고, 같은 항목의 부분 결과는 롤백하며, 전부 실패 시에도 첫 오류를 그대로 던져 단일 체인 의미를 유지(이전에는 손상된 ELC 1건이 전체 묶음을 전멸시켰음)

### 🐛 RC2 수정: 안정성 4건(Batch-2)

([Fichero.vb](../Clases/Fichero.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb))

- 100% 보충 MD5를 전역 잠금에서 분리: 잠금 안에서는 예약만 수행(`ComprobandoMD5` 설정으로 중복 투입 방지), 잠금 밖 스레드 풀에서 실행(이전에는 GB 파일이 동시에 걸리면 UI/스케줄러가 멈췄음)
- Web 암호 무작위 IV를 설정 중복 비교에서 제외하고 평문 스냅샷 비교로 변경(이전에는 Web 암호가 있으면 `Configuration.xml`이 5초마다 반드시 다시 기록되었음)
- 종료 대기에 `CreandoLocal/Verificando/Descomprimiendo/ComprobandoMD5` 보완; 검증 쓰기 저장과 `GuardarXML`이 함께 `FicheroDownloader`를 보유(이전에는 종료 시 큐가 찢어졌음)
- 120초 워치독 판정을 30초 배출 후에 수행하도록 이동, 배출 중 완료된 건은 더 이상 실패로 오보하지 않음(이전에는 성공/실패 이중 이벤트 경쟁으로 온전한 파일이 오류로 고정될 수 있었음)

### 🐛 RC2 수정: 경험 및 심층 방어(Batch-3)

([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [StreamingModule.vb](../HttpModule/StreamingModule.vb) / [Main.vb](../Forms/Main.vb) / [Updater.vb](../Clases/Updater.vb) / [Criptografia.vb](../Clases/Criptografia.vb) / [SteganoManager.vb](../Stegano/SteganoManager.vb))

- 정규식 싱글턴화(`Compiled` 공유 인스턴스): `URLExtractor` 전체 테이블 스캔, `MegaFolderHelper` 노드별 처리, `StreamingModule` 요청별 처리가 더 이상 `New Regex`를 수행하지 않음(이전에는 수백 개 링크 붙여넣기 시 UI가 멈췄음)
- fragment 디코딩: `UnescapeDataString` + 공백 제거, `%23/%3D` 이스케이프 체인이 더 이상 영구적인 "복호화 불가"가 되지 않음; 데드 `Contains(" ")` 분기 삭제
- 업데이트 URL은 https만 허용; `version.xml`은 외부 엔터티(XXE) 금지; 공개 체인은 검증을 건너뛰고 Warning으로 흔적 유지; Stegano 원격은 64MB+30초 상한; streaming의 비정상 mega 매개변수는 500이 아닌 400 반환

### 🐛 RC2 수정: 병합 리뷰 패치(Pre-merge Gate)

- 좌측 열 작업 개요+바로가기 진입: 전체 속도/개수/큐 진행률/남은 시간, 기존 430ms 새로고침 루프 재사용(신규 타이머 없음); 압축 해제 큐/스트리밍 라이브러리/로그를 첫 화면으로 이동
- 스트리밍 라이브러리 Web 가져오기에 항목별 격리 추가: 손상된 폴더/만료된 ELC는 해당 항목만 건너뜀(이전에는 요청 전체가 무응답이었음)
- 할당량 동일 이벤트 중복 제거: 서킷 브레이커 기간 중 중복 보고는 등급을 올리거나 대기를 연장하지 않음(이전에는 다중 연결 동시 적중 시 순식간에 6h 등급으로 점프했음)
- CI 단일 파일 검증을 Windows PowerShell 5.1에서 실행하도록 변경(이전에는 `pwsh`에서 ReflectionOnlyLoad가 반드시 예외를 던져 빌드가 항상 빨간불이었음)

### 📦 버전 번호

- Assembly / FileVersion → `2.5.0.0`(RC는 건드리지 않음)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC2` / `VERSION_UPDATE` → `2.5`(숫자는 그대로 유지, beta/RC 기간에는 업데이트를 알리지 않음; `docs/version.xml`은 `2.4.7.0` 유지, 정식 버전에서만 올림)

***

## [2.5 RC1] - 2026-09-12

익명 다운로드 MEGA 할당량(HTTP 509 / API -17) 특화 대응. 핵심 주제: **할당량을 예측 가능하게——자동 일시 중지, 정직한 카운트다운, 시간이 되면 자동 복구**.

### ✨ 신기능: 할당량 전역 서킷 브레이커 + 카운트다운 배너

([MegaQuotaManager.vb](../Clases/MegaQuotaManager.vb) 신규 / [Conexion.vb](../Clases/Conexion.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Main.vb](../Forms/Main.vb))

- 509와 -17 통합 식별: 판정은 `HttpStatusCode = 509`(상태 코드)와 `-17 / EOVERQUOTA`(API 의미)만 사용하고, 응답 본문 텍스트 매칭은 수행하지 않음. 파일 정보(:429 예외 경로 + :364 숫자 경로)와 폴더 읽기(:47 예외 경로 + :51 숫자 경로) 두 지점을 모두 포함
- 점진적 서킷 브레이커: 첫 적중 시 60분 일시 중지, 할당량 기간 중 반복 적중 시 2시간으로 상향, 최대 6시간; `Retry-After`가 있으면 큰 값을 사용(대부분 없으므로 의존하지 않음)
- 서킷 브레이커 기간: 새 작업을 시작하지 않고, 새 파일 정보 검증을 수행하지 않음(검증 1회=API 호출 1회이며 처벌 윈도우를 연장시킴), 청크 실패 시 16초 헛되이 재시도하지 않고 스케줄러가 일괄 대기
- 메인 화면 배너: `MEGA 할당량이 소진되었습니다(정확한 복구 시간이 제공되지 않음). 자동으로 일시 중지되었으며, X시간 Y분 후 자동으로 재시도합니다` + **[지금 다시 시도]** 버튼(IP 교체 / 프록시 / 라우터 재시작 후 수동 해제 가능, 잠기지 않음)+ 상태 표시줄 카운트다운 + 진입/해제 시 각각 한 번씩 트레이 풍선 알림

### ✨ 개선: 실패 자동 복구 기본 활성화(1회성 마이그레이션 포함)

([Configuracion.vb](../Clases/Configuracion.vb)) `ResetearErrores` 기본값이 켜짐(15분)으로 변경됨; 기존 설정은 `ResetearErroresMigratedV25`를 통한 1회성 마이그레이션으로 켜짐 상태로 전환되며, 이후 수동 선택은 덮어쓰지 않음; 새로 설치하면 바로 켜짐.

### ✨ 개선: 영구 실패는 자동 복구에서 제외 + 일괄 작업

([Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb)) `-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED`는 `EsErrorPermanente`로 표시되며, 자동 복구에서 건너뜀(더 이상 15분마다 되살아나 로그를 남기고 할당량을 소모하지 않음); 마우스 오른쪽 클릭에 **실패한 항목 모두 다시 시도**와 **실패한 항목 모두 제거**(확인란에 개수 표시, 로컬 `.part` 동시 삭제 선택 가능) 신규 추가.

### ✨ 개선: 대용량 폴더 읽기 진행률 피드백

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [AddLinks.vb](../Forms/AddLinks.vb)) 폴더 파싱을 UI 스레드에서 분리하고, 진행 창에 `폴더를 읽는 중…N개 항목 파싱됨`을 실시간 표시하며, 취소 지원(결과를 버리고 패키지에 추가하지 않음); 온라인 시청도 비동기 파싱 경로를 사용.

### 🐛 RC1 수정: 할당량 오보 + 재시작 시 오류 유실 + 진행률 표시줄 회색 문제(P1)

- 할당량 서킷 브레이커 기간 중 시간 초과는 `MegaQuotaExceededException`으로 보고(이전에는 120초 만료 시 일반 시간 초과로 보고되어 `FailedByQuota=False`가 되었고, "지금 다시 시도"로 회수 불가); 비할당량 120초 문구에서 "링크 만료" 오해를 유발하는 표현을 제거하고 총 시간 워치독(유휴 시간 초과가 아님)임을 명확히 하며, 진행률은 이어받기 지점으로 유지
- `DescripcionError / EsErrorPermanente / FailedByQuota`를 큐 XML에 영속화(4000자 자름), 재시작 후 "오류 보기"가 더 이상 비어 있지 않으며, 영구 실패가 자동 복구에 의해 반복적으로 되살아나지 않음; 기존 큐는 설명 텍스트로 표시를 역추적하여 호환; 빈 설명은 조작 가능한 대체 팝업 표시, 더 이상 빈 창 없음
- 진행률 표시줄 사용자 지정 그리기: 배경을 투명하게 하여 행 배경색이 드러나도록 함(밝은 테마에서 전체가 회색이 되는 현상 제거, 0% 행이 더 이상 순수 회색이 아님); 그라데이션 없이 `FillColor` 단색으로 채움; 테두리는 테마 `Border`를 따름; 막대 높이 18; 작은 진행률은 최소 2px 보장
- 다운로드 목록 열 너비 손상 방지: 10개 열에 `Minimum/MaximumWidth` 추가(# 20–40/파일 이름 150–700/나머지는 코드 참조), `ColumnWidthChanging`에서 드래그 중 값을 클램프(OLV는 헤더 드래그를 가로채지 않음); 시작 시 손상된 상태(`#`/파일 이름이 0으로 눌림, 단일 열 >800, 표시 총 너비 >2000)는 자동으로 초기화 후 디스크에 저장; 마우스 오른쪽 클릭에 "기본 열 너비 복원" 추가(이전에는 `#` 열이 `Hideable=False` + 마이그레이션이 이미 실행되어 프로그램 내에서 기본값으로 돌아갈 진입점이 없었음)

### 🐛 RC1 패치: 배너 레이아웃/할당량 의미/빌드 사용 가능성

- 빌드 사용 가능성: 최신 MSBuild가 resx를 preserialized 형식으로 컴파일하여 시작 즉시 충돌(`My.Resources.icono` 위치에서 `FileLoadException`, 전역 예외 보호에 삼켜져 조용한 exit 0이 됨). `Resources\DLLs`에 `System.Resources.Extensions/Memory/Buffers/Unsafe/Numerics.Vectors` 신규 추가, vbproj에 참조 추가, `app.config`에 버전 리디렉션 추가, CI 산출물 목록 동기화
- 할당량 배너를 절대 레이아웃으로 변경: 목록+양쪽 사이드바 Top/Height는 항상 도구 모음 하단+배너 표시 여부로 다시 계산하며, 상대 이동과 Bottom 앵커를 제거(이전에는 크기 조정/최대화/DPI 변경 후 숨기면 배너 높이만큼 여백이 남아 상태 표시줄을 가렸음)
- 할당량 만료 깨우기가 자동 복구 스위치에서 분리됨: 서킷 브레이커 해제 시점에 바로 `WakeQuotaFailedItems` 호출(이전에는 `If ResetearErrores` 안에 숨어 있어, 자동 복구를 끈 사용자는 배너가 사라져도 실패 항목이 영원히 복구되지 않았음)
- 처벌 점진 로직 수정: 등급은 24h 후 감쇠하며, 만료/수동 초기화 시 0으로 돌아가지 않음(이전에는 점진이 절대 진행되지 않았고, 수동 재시도 시 60min으로 되돌아간 뒤 즉시 요청을 보냈음); `Retry-After`는 HTTP-date 지원
- 카운트다운 문구: 120초 이내에는 초 단위 카운트다운, 그 이상은 내림(이전에는 마지막 60초 동안 "1 min"에 고정되었고, 1h59m30s가 "2 h 0 min"으로 표시되었음); 배너 색상을 테마 시스템에 편입(`QuotaBack/QuotaFore`)
- Toast는 새로 만들기/재사용 모두 네 변 클램프를 공유(이전에는 첫 팝업이 화면 가장자리에 붙을 때 절반이 화면 밖에 있었음); 폴더 진행 창의 진행률 표시줄은 시각적 스타일을 제거하고 테마를 따르며, 취소는 진짜 취소(`CancellationToken`을 파싱 루프에 전달)
- 스케줄 루프 단일 지점 오류: 반복 단위 Try/Catch + `RunWorkerCompleted` 워치독 3초 자가 복구 재시작(이전에는 예외 1회가 새로고침+배너+복구를 모두 멈추게 했음); 서킷 브레이커 기간 중 시작 버튼 클릭 시 Toast 알림 제공(이전에는 클릭해도 무반응이었음)

### 🐛 RC1 수정: UI 체감 결함(12건)

- `ThemeManager`에 `ListBox / CheckedListBox / ToolTip` 분기 신규 추가(이전에는 다크 모드에서 칠해지지 않아 각 창마다 수동 패치로 땜질했음); 최상위 `MainMenu`는 여전히 시스템 메뉴이므로 알려진 제한으로 기록
- Toast 재사용 시 위치를 다시 계산하여 화면을 벗어난 만료 좌표 방지; 할당량 배너 Top은 도구 모음+DPI 배율 추적; 도구 모음 아이콘/탐색 행 높이는 DPI 추적; 상태 표시줄 RAM/Proc은 자동 너비로 변경(Designer 90→150); 기본 창 너비 880→1024로 확대하여 Nombre 열 확보
- 설정 검색이 `NumericUpDown / ListBox / DataGridView`와 ComboBox 후보 항목을 지원하며, Label에 포커스를 둘 수 없을 때는 부모 컨테이너로 포커스를 대체하고, 일치 항목이 없으면 비프음으로 피드백; 설정 Cancel은 `Bottom|Right` 앵커로 변경; ELC 테이블은 스킨 변경 후 다시 그려 첫 프레임 잔상 방지
- AddLinks: 텍스트 비우기 시 `HiddenLinks`도 함께 비우고, 자리 표시자 행 수를 개수에 포함; 워터마크 회색 글자는 다크 모드에서도 보이도록 수정; 압축 해제 암호 `MaxLength` 6→128(2곳); ELC 두 Label은 기존 키 번역 재사용; Streaming 빨간 글자는 테마 `ErrorFore`로 변경+잘못된 링크는 알림 제공; `btnLanzarVLC.DialogResult`를 None으로 변경하여 창이 잘못 닫히는 것 방지; Credits에서 `AcceptButton=lblTitle` 제거+빈 번역 원시 key 노출 보호

### 🌐 언어 추가

`en-US` / `zh-CN`에 `Quota_Banner / Quota_Status / Quota_RetryNow / Quota_Recovered / Quota_Error / Retry all failed / Remove all failed(+confirm/delete part) / Folder_Reading(+Count)` 신규 추가; RC1에서 `es-ES`에 같은 11개 키 보완(다른 언어는 en-US 대체를 통해 표시).

### 📦 버전 번호

- Assembly / FileVersion → `2.5.0.0`(RC는 건드리지 않음)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC1` / `VERSION_UPDATE` → `2.5`(숫자는 그대로 유지, beta/RC 기간에는 업데이트를 알리지 않음; `docs/version.xml`은 `2.4.7.0` 유지, 정식 버전에서만 올림)

***

## [2.4.7] - 2026-09-12

업데이트 알림을 3개 옵션으로 업그레이드 + 폐기된 검색 엔진 통합 제거. 핵심 주제: **"알림 빈도" 선택권을 사용자에게 돌려주고, 죽은 도메인을 정리**.

### ✨ 개선: 업데이트 알림 3개 옵션

([Main.vb](../Forms/Main.vb) / [Configuracion.vb](../Clases/Configuracion.vb)) 새 버전 팝업이 "예/아니요"에서 3개 옵션으로 업그레이드: **예**=지금 업데이트; **아니요**=3시간 후 다시 알림; **취소**=현재 버전을 다시 알리지 않음. `UpdateSkipVersion` 설정 항목에 "다시 알리지 않음" 선택을 영속화(버전별로 기록되며 해당 버전만 차단; 향후 새 버전 출시 후 자동으로 알림 복원), 선택 후 즉시 디스크에 기록. 설정의 "업데이트 확인" 체크박스는 여전히 전체 스위치(해제 시 검사를 완전히 중지).

### 🧹 유지보수 정리: 폐기된 검색 엔진 통합 제거

원본 "옵션 → 찾기" 메뉴가 모았던 4개 MEGA 검색 엔진 도메인(megafiles.me / megafindr.com / megasearch.co / megasearch.co.nz)이 모두 서비스 종료되어 전체 기능이 동작하지 않음:

- ([URLExtractor.vb](../Clases/URLExtractor.vb)) `mega://mega-search?...` 링크 파싱 삭제(`MEGASEARCHPREFIX` 상수, 정규식 패턴, `CheckFileIDAndFileKey`의 mega-search 파싱 분기)
- ([Main.vb](../Forms/Main.vb)) "찾기" 메뉴 생성 및 `Buscador_Click` 삭제
- ([InternalConfig.xml](../Resources/InternalConfig.xml)) `SEARCH_LIST`(죽은 도메인 4개)와 `MEGA_SEARCH_CURL` 삭제
- ([InternalConfiguration.vb](../Clases/InternalConfiguration.vb)) 호출자가 없는 `ObtenerValuesFromInternalConfig` 삭제
- 10개 언어 파일에서 `Searc&h` 데드 키 삭제

### 🌐 언어 추가

`en-US`/`zh-CN`/`zh-TW`/`es-ES`에 `Update prompt hint`(3개 옵션 팝업의 버튼 의미 설명, 다른 언어는 en-US 대체를 통해 표시) 신규 추가.

***

## [2.4.6] - 2026-09-04

7건 가짜 성공/조용한 실패 수정 + 1건 유지보수 정리. 핵심 주제: **실패는 실패답게 드러나도록**.

### 🐛 수정: 재시작 후 가짜 성공(P1)

([Paquete.vb](../Clases/Paquete.vb)) `MarcarFicherosComoParados`가 `Verificando`/`Descomprimiendo` 상태 파일을 `Completado`로 표시했음——그러나 `Verificando`는 다운로드 전의 일시적 상태(재시작 후 `EstadoAnterior`가 이미 유실됨)이며, `Descomprimiendo`는 다운로드는 끝났지만 압축 해제가 끝나지 않은 상태로, **두 경우 모두 "1바이트도 디스크에 기록되지 않았는데 성공 보고"일 수 있음**. 이제 일괄적으로 `EnCola`로 되돌림: 이어받기로 계속 진행하며, 이미 완전한 파일은 빠른 체크섬만 수행하고 처음부터 다시 받지 않음.

### 🐛 수정: 설정 저장 가짜 성공

([Configuration.vb](../Forms/Configuration.vb)) `Config.GuardarXML` 실패 시 하위 계층에 로그만 남기고 `ErrorConfig`를 설정하는데, UI는 그대로 "저장 성공" 팝업을 표시하고 창을 닫았음. 이제 저장 후 `ErrorConfig <> SinErrores`를 확인하면 오류 팝업을 표시하고 창에 머무르며, 더 이상 성공으로 표시하지 않음. 오류 문구 언어 키 신규 추가.

### 🐛 수정: 대문자 링크가 조용히 버려짐

([URLExtractor.vb](../Clases/URLExtractor.vb)) `ExtraerURLs`가 `IgnoreCase`로 `HTTPS://MEGA.NZ/...`를 매칭한 뒤, 곧바로 대소문자 구분 방식의 `ExtraerFileID` 검증을 호출하여 실패하자마자 버렸음——사용자가 대문자 링크를 붙여넣으면 아무런 반응이 없었음. 6곳의 `New Regex(pattern)`에 모두 `IgnoreCase` 보완; 추가로 `#F`/`#N` 패턴 비교를 `ToUpperInvariant`로, `fenc`/`enc`/`mega-search` 접두사 매칭을 모두 `OrdinalIgnoreCase`로 변경. 캡처 그룹은 원래 대소문자를 유지하므로 FileID/FileKey의 대소문자 구분에는 영향이 없음.

### 🐛 수정: 비정상 enc 링크 충돌

([URLExtractor.vb](../Clases/URLExtractor.vb) / [ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) base64url 길이가 `%4==1`인 값은 불법이며, 원래 `"==".Substring(3)`에서 `ArgumentOutOfRangeException`이 그대로 충돌했음. 이제 사전에 판정하여 친화적인 오류를 던지고, 일괄적으로 "링크가 잘못됨" 계열 알림을 표시.

### 🐛 수정: 폴더 API 빈 응답 NRE

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb)) `DeserializeObject`에 Try가 없고 `FileList.f`를 바로 순회했음——비정상 응답(빈 문자열/프록시 HTML)에서 NRE 발생. 이제 Try/Catch + 빈 값 검사를 추가하고, 일괄적으로 "잘못된 서버 응답" 오류를 던지며, HTML 내용은 로그에 남기지 않음.

### ✨ 개선: 하위 폴더 링크를 ELC로 변환해도 범위 유실 없음

([ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) `MegaLink`에 `SubFolderID`/`SubFileID` 추가; `ServerEncode`는 MegaFolder인 경우 `/folder/하위ID` 또는 `/file/파일ID` 접미사를 추가. 구버전 디코더는 접미사를 무시(전체 폴더로 축소=기존 동작)하고, 신버전 디코더는 하위 범위를 복원하므로 **양방향 호환**. `ExtraerSubFolderID`/`ExtraerSubFileID`는 구식 토큰(`mega://#F!...!/folder/...`, ELC 디코딩 산출물)에 대한 접미사 대체 파싱을 보완. `enc`/`enc2` 암호문에는 하위 범위를 저장할 곳이 없으므로, `EncodeLinksForm`은 하위 범위를 포함한 링크는 인코딩을 건너뛰고 원문을 유지하여 조용히 전체 폴더로 확대되는 것을 방지.

### 🌐 언어 키 누락 보완

`en-US`/`zh-CN` 각각 +5: `ELC created successfully`, `URL is mandatory`, `VLC path is not valid`, `Open &ELC`, `Configuration could not be saved...`. 이전에는 키 누락 시 `Language.GetText`의 en-US 대체를 통해 영어 원문이 표시되었으며, 충돌은 아니지만 중국어 화면에서 번역 누락이었음.

### 🧹 유지보수 정리

- `docs/BUGFIX-CHECKLIST.md` 삭제(2026-07-13 검토 체크리스트가 심하게 오래되었으며, 최소 8곳의 ⬜이 실제로는 이미 수정되었음)
- `Resources\DLLs\xunit.dll` 삭제(vbproj 참조 없음, Fadd.dll의 xunit 1.0.3 의존성은 원래부터 해석 불가했으며, 디스크의 1.9.1 버전은 한 번도 사용되지 않았음)
- vbproj에서 데드 참조 `TODO\TODO.txt` 삭제
- README: Web 화면 설명을 "기본값은 127.0.0.1에만 바인딩되며, LAN 액세스를 켜고 바인딩 IP를 지정할 수 있음"(구현과 일치)으로 정정; xUnit 관련 항목 제거

***

## [2.4.5] - 2026-09-02

본 버전은 전면 코드 리뷰 후 시스템 수정입니다: 확인된 36건 문제를 모두 처리했으며, 충돌 수정, 기능 정확성, HTTP 프로토콜 준수, 리소스 누수 및 보안 강화를 포함합니다.

### 🛡️ 전역 예외 보호

([ApplicationEvents.vb](../ApplicationEvents.vb)) 이전에는 애플리케이션 전체에 미처리 예외 보호가 전혀 없었음——UI 스레드 예외가 발생하면 바로 .NET 충돌 대화 상자가 뜨고 프로세스가 종료되었으며, 백그라운드 스레드 예외는 프로세스를 조용히 사라지게 했음. 이제:

- `My.UnhandledException`: 예외를 로그에 기록한 뒤 **종료하지 않으며**, 사용자가 상태를 저장할 기회를 가짐

- `AppDomain.UnhandledException`: 백그라운드 스레드 예외도 최소한 로그 단서를 남김

### 🐛 수정: 목록 새로고침 충돌(고빈도 충돌 원인)

([Main.vb](../Forms/Main.vb)) 다운로드 상태/백분율/예상 시간/진행률 텍스트 4개 `AspectGetter`의 Catch 블록이 영어 스택을 팝업으로 표시한 뒤 `Throw`했음——그런데 AspectGetter는 **매 행을 다시 그릴 때마다 실행**되므로, 행마다 창이 한 번씩 뜨다가 닫으면 충돌했음. 로그만 기록하고 안전한 플레이스홀더 값을 반환하도록 변경.

### 🐛 수정: 나머지 사용자 가시 오류

| 증상                                    | 원인 및 수정                                                       |
| ------------------------------------- | ----------------------------------------------------------- |
| ELC 열기에서 "취소" 클릭 시 "The path is not valid" 보고 | 취소 시에도 빈 문자열로 `AddDLC` 호출했음; 이제 조용히 종료                                   |
| 단일 파일에서 Reset 클릭 시 곧바로 Error로 복귀                | Fichero 분기에서 `ResetearDescarga()` 호출이 누락되어 `.part`와 오류 상태가 잔류했음; 패키지 분기와 동일하게 보완 |
| 첫 실행 강제 설정이 "취소"로 우회 가능                      | 암호 입력란에 자리 표시자 `*****`가 채워져 항상 비어 있지 않으므로 검증에 절대 도달할 수 없었음; 자리 표시자는 이제 "미설정"으로 간주                     |
| Web 시간 초과 61-99 저장 시 다시 열면 비어 있다가 5로 변경됨              | 불러오기 경계 `>60`과 저장 경계 `0-99`가 불일치; `0-99`로 통일                        |
| 큐 파일 손상으로 시작 즉시 충돌                          | `CDate(strFecha)`가 문화권에 의존하고 Try가 없었음; `Date.TryParse` 이중 문화권 파싱으로 변경하고 손상된 값은 건너뜀  |
| 백그라운드 팝업이 주 창 뒤에 숨어 멈춘 것처럼 보임                        | DoWork 스레드에서 직접 `MessageBox.Show` 호출; `SafeShowError`를 통해 UI로 마샬링하도록 변경     |
| 3개 백그라운드 worker 오류 시 전체 화면 영어 스택 팝업                | 스택은 이미 로그에 기록되었으며, 사용자에게는 `ex.Message`만 표시                                    |
| VLC 시작 실패 시 아무런 피드백 없음                         | 반환값이 무시되고 Try가 없었음; 두 호출 지점에서 이제 반환값을 확인하며, `WatchOnline` 내부에서 보호                 |
| 비 ELC/DLC 파일을 끌어다 놓으면 조용히 버려짐                   | 이제 알림을 표시                                                      |
| 항목을 선택하지 않고 "디렉터리 열기" 클릭 시 빈 경로 오류                    | 빈 경로는 바로 종료                                                     |

### 🐛 수정: 기능 정확성 결함

| 위치                                                               | 수정                                                                                                                                                      |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DescompresorController.vb](../Clases/DescompresorController.vb) | **볼륨 누락 RAR 가짜 성공**: `IsComplete=False`인데 조용히 건너뛰고 예외를 설정하지 않아 상위에 "압축 해제 성공" 보고; 이제 명시적으로 오류 발생. **중복 Code가 "압축 해제 중"에서 멈춤**: 조용히 건너뛰고도 True 반환; 이제 기존 큐 항목을 업데이트                                                |
| [FileDownloader.vb](../Clases/FileDownloader.vb)                 | **UI 멈춤 최대 16.5초**: 청크 실패 백오프의 `Thread.Sleep` 바쁜 대기가 UI 스레드에서 실행; 스레드 풀로 이동. **"Must specify size"가 실제 오류를 가림**: Size=0 시 마무리 블록 건너뜀. **Dispose가 Mutex/MutexFile/trigger를 절대 해제하지 않음**: 이제 확정적으로 해제 |
| [Configuracion.vb](../Clases/Configuracion.vb)                   | Web 암호 복호화 실패의 빈 Catch: 암호문을 그대로 암호로 사용하여 로그인이 영원히 실패하고 단서도 없었음; 이제 로그를 기록하고 비움                                                                                                          |
| [Conexion.vb](../Clases/Conexion.vb)                             | FileID를 이스케이프 없이 JSON/URL에 직접 이어 붙였음; JSON 이스케이프 + UrlEncode 보완                                                                                                            |
| [Main.vb](../Forms/Main.vb)                                      | "패키지+하위 파일" 삭제 시 `CancellationComplete`이 이중으로 걸려 `Dispose`가 두 번 실행되었음; 이미 제거된 개체는 바로 건너뜀                                                                                       |

### 🌐 HTTP 모듈: Range 준수 + CSRF + 속도 제한

- **RFC 7233**([StreamingModule.vb](../HttpModule/StreamingModule.vb)): `bytes=-N` 접미사 구간과 `bytes=N-` 개방 구간 지원; 범위 초과 시 416 + `bytes */size` 반환(이전에는 조용히 클램프); `bytes=0-0` 단일 바이트 탐색은 정렬된 블록 하나만 가져옴(이전에는 MEGA에 전체 파일을 요청하여 대역폭 증폭); 마지막 블록을 Content-Length까지 자름(응답 본문을 초과하여 보내지 않음)

- **`?mega=`** **파싱**: 프레임워크가 파싱한 매개변수 값을 직접 사용하며, `?p=암호&mega=...` 순서가 더 이상 실패하지 않음

- **스트리밍 라이브러리 CSRF**([StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb)): Delete/Save/OpenVLC/ImportLinks/ExportLinks에 POST + 유효 토큰 요구(Web 화면의 EnsureCsrf 패턴 재사용); 템플릿에 토큰 주입; **탐색 페이지 OpenVLC가 GET으로 POST 전용 인터페이스를 호출하던 기존 오류도 함께 수정**

- **로그인 속도 제한**([WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb)): PBKDF2(100k)가 요청 스레드에서 동기 실행되므로 POST 폭격 시 스레드 풀이 가득 찰 수 있음; 이제 동시 실행 상한 4 + 60초 윈도우 실패 잠금 10회 적용

- 3곳의 `StreamWriter(response.Body)`에 `Using` + BOM 없는 인코딩 보완

### 💾 리소스 누수

- `Criptografia.decrypt_key`: 루프 안의 `CreateDecryptor`를 절대 Dispose하지 않아(key 복호화마다 N개 ICryptoTransform 누수); Using으로 변경

- `MD5Utils.MD5CalcString`: Using 보완

- `DescompresorController` ×4곳, `ThrottledStreamController` ×1곳 Mutex에 Try/Finally 보완(프로젝트 규칙에 부합)

- Configuration/PropiedadesDescarga 총 5곳의 호버 ToolTip 누수; 단일 인스턴스 재사용

### 🔒 Stegano(스테가노그래피)

- **먼저 손상된 파일을 쓰고 나중에 오류 보고**: 용량 검증이 디스크 기록 후에 수행되어 잘린 .jpg가 남았음; 메모리 인코딩+검증 전부 통과 후 디스크에 기록하도록 변경

- **OpenWrite로 꼬리를 비우지 않음**: 더 긴 이전 파일을 덮어쓸 때 기존 바이트와 새 바이트가 이어 붙었음; `WriteAllBytes`로 잘라 덮어쓰기로 변경

- **Uri 검증이 유명무실**: `RelativeOrAbsolute`는 "hello world"에도 True 반환; Absolute + http/https/file로 한정

### 🧹 유지보수 정리

- vbproj에서 데드 참조 `TODO\TODO.txt`, `plantilla botones.psd` 삭제; 고아 파일 `postbuildevent.xml` 삭제

- DPI 설정을 PerMonitorV2로 통일(app.config에 `DpiAwareness` 보완, myapp HighDpiMode=2); 배포되지 않은 Unsafe 어셈블리 리디렉션 제거

- README에서 존재하지 않는 xUnit 기술 스택 항목 삭제

- 하드코딩된 영어 메시지(ELC/DLC 오류, Invalid input data 등 10곳)를 언어 시스템에 연결, en-US/zh-CN 항목 신규 추가

- BUGFIX-CHECKLIST.md 머리글에 "오래됨" 경고 추가(최소 8곳의 ⬜이 실제로는 이미 수정되어 중복 작업을 방지)

***

## [2.4.4] - 2026-09-01

### ✨ 신기능: 하위 폴더 링크 다운로드

**이전**: `mega.nz/folder/<루트ID>#<키>/folder/<하위ID>` 형식 링크가 루트 폴더 링크로 취급되어 루트 폴더 전체 내용을 모두 다운로드했음.

**현재**([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb)):

| 링크 형식                               | 동작                         |
| ---------------------------------- | -------------------------- |
| `mega.nz/folder/루트ID#키/folder/하위ID` | 지정된 하위 폴더 내용만 다운로드(경로는 하위 폴더를 기준으로 재지정) |
| `mega.nz/folder/루트ID#키/file/파일ID`  | 지정된 단일 파일만 다운로드                  |
| `mega.nz/folder/루트ID#키`            | 전체 루트 폴더 다운로드(변경 없음)               |

- 정규식을 확장하여 `/folder/<하위ID>`와 `/file/<파일ID>` 접미사를 캡처

- 파일 목록을 부모 노드 체인을 따라 위로 순회하며 필터링하고, 대상 하위 노드에 속한 파일만 유지

- 다운로드 경로를 하위 폴더 상대 경로로 재지정하며, 더 이상 불필요한 상위 디렉터리 계층이 나타나지 않음

### 🐛 수정: 하위 폴더 링크 다운로드 완료 후 MetaMAC 오류 오보(사용자 실측 확인)

**증상**: 하위 폴더 링크가 100% 다운로드된 뒤 MetaMAC 체크섬 실패 오류 팝업 표시; 파일 내용은 실제로 완전했음.

**원인**: 각 데이터 청크의 CBC-MAC 계산에 **0 초기 IV**를 사용했음. 그런데 MEGA 실제 알고리즘(SDK `SymmCipher::ctr_crypt`)의 청크 MAC 초기값은 **파일 nonce를 두 번 복사**한 값——key의 4-5번째 워드(word)를 이어 붙인 16바이트 `[n0, n1, n0, n1]`임. 0 IV로 계산한 MAC은 실제 업로드된 파일의 MetaMAC과 절대 일치할 수 없으므로, 8 words key(MetaMAC 내장)가 포함된 다운로드는 반드시 오류가 발생했음; 단일 파일 공개 링크(4 words key, 검증 건너뜀)는 영향이 없어 문제가 그동안 가려져 있었음.

**수정**([Criptografia.vb](../Clases/Criptografia.vb)):

```vb
' 修复前:零 IV(必然校验失败)
Dim chunkMac As Integer() = New Integer() {0, 0, 0, 0}
' 修复后:nonce(key 第 4-5 word)复制两份,与 SDK 一致
Dim chunkMac As Integer() = New Integer() {nonceWords(0), nonceWords(1), nonceWords(0), nonceWords(1)}
```

나머지 부분(0 초기값 폴딩, `(m0^m1, m2^m3)` 최종 압축, 128 KiB × i 청크 스케줄)은 SDK `macsmac`/`ChunkedHash` 소스와 한 줄씩 대조하여 원래부터 정확했음을 확인했으며, 변경하지 않았음.

### 🔧 MetaMAC 체크섬을 SDK 표준 동작에 맞춤

검증 함수의 "각 청크 경계마다 미리 MAC을 확인하고 접두사 일치를 허용"하는 관용 로직 삭제——SDK 권위 구현(`generateMetaMac` + `macsmac`)은 **전체 파일을 다 읽은 뒤 한 번에 전체 비교**를 수행함. 접두사 일치는 알고리즘 오류 시대의 오판 산물이므로 함께 제거; 어느 위치의 실제 손상도 여전히 하드 실패 처리됨.

### 🔒 9건 보안 강화

| 위치                                             | 수정                                                                   |
| ---------------------------------------------- | -------------------------------------------------------------------- |
| `Criptografia.StripNullCharacters`             | `Replace(vbNullChar, "")`로 다시 작성, 문자별 이어 붙이기로 인한 위치 오프셋 오류 제거                       |
| `Criptografia.AES_EncryptString/DecryptString` | 실패 시 빈 문자열이 아닌 `Nothing` 반환; 영속화 지점(Configuracion/Fichero)에서는 암호화 실패 시 쓰기를 건너뛰고 이전 값을 유지하며 더 이상 조용히 비우지 않음 |
| AES 암호문 형식                                       | 무작위 IV 형식 `{1}\|\|IV\|\|암호문` 신규 추가, 기존 형식과 자동 양방향 호환                              |
| `FileDownloader`                               | MetaMAC 불일치 시 예외 발생 및 청크 재시도 초기화(이번 알고리즘 수정과 함께 더 이상 오보 발생하지 않음)                               |
| `Criptografia.GetFileKeyFromPreSharedKey`      | PSK에 비 ASCII 문자(>255)가 포함되면 로그 기록 후 `Nothing` 반환, 키 스트림 어긋남 방지                        |
| `WebInterfaceModule`                           | Web 암호 저장을 무작위 솔트 + PBKDF2(100k 라운드) 파생으로 변경, 솔트 없는 MD5 대체                             |
| `StreamingModule` / `StreamingLibraryModule`   | 암호 비교를 `Criptografia.FixedTimeEquals` 고정 시간 비교로 변경, 타이밍 부채널 방지                   |
| `ClientConnected` 리플렉션                           | 정적 `MemberInfo` 캐시 + null 검사 + Try/Catch, 실패 시 "연결된 것으로 가정"으로 강등                |

### 📦 버전 번호

- Assembly / FileVersion → `2.4.4.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.4`

- `docs/version.xml` → `2.4.4.0`

***

## [2.4.3] - 2026-08-26

### ✨ 신기능(Issue #1)

| 기능           | 설명                                                                                                                                                                               |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 7z 압축 해제 지원      | SharpCompress는 7z 컨테이너를 지원하지 않아(이전에는 모든 .7z 압축 해제가 반드시 실패했음). 이제 시스템에 설치된 7-Zip CLI를 우선 호출하며, 미설치 시 내장 7zr.exe(퍼블릭 도메인, 7-Zip 공식 경량 버전)를 `%LOCALAPPDATA%\MegaDownloader\bin`에 해제하여 사용. 암호 및 multipart 분할 볼륨(.7z.001/.002/...) 지원 |
| Web 서버 LAN 푸시 | 설정 → Web 서버에 "LAN 액세스 허용" 스위치 신규 추가(기본값 꺼짐). 기본값은 여전히 127.0.0.1에만 바인딩; 켜면 모든 네트워크 어댑터에 바인딩되어 휴대폰/LAN 장치가 브라우저를 통해 다운로드를 푸시할 수 있음. 켜면 서버 암호 설정(8자 이상)을 강제하며, 설정 저장과 서버 시작 이중 검증을 수행                                                              |
| LAN 사용자 지정 바인딩 IP  | "LAN 액세스 허용"을 켠 뒤 "바인딩IP(비워 두기=전체)" 입력란 신규 추가: 비워 두면 모든 네트워크 어댑터에서 수신; 지정 IP(예: `192.168.1.100`) 입력 시 해당 어댑터에서만 수신하며, 다중 네트워크 어댑터/가상 어댑터 환경에서 노출 범위를 정밀 제어. 저장과 시작 이중 IP 형식 검증, 잘못된 주소는 시작을 거부하고 오류 보고                                                         |

### 🐛 수정: 클립보드 모니터링 웹페이지 복사 누락(Issue #1)

**증상**: 웹페이지에서 MEGA 링크를 복사해도 추가 창이 뜨지 않으며, 일부 앱 내 Ctrl+C에서만 동작했음.

**원인**: 브라우저(Chrome/Edge/Firefox)는 **지연 렌더링**을 사용——클립보드 변경 알림이 도착한 시점에는 데이터가 아직 실제로 기록되지 않았음; 즉시 읽으면 빈 값을 가져오거나 `CLIPBRD_E_CANT_OPEN`(클립보드가 여전히 원본 프로세스에 의해 점유됨) 오류 발생.

**수정**([Main.vb](../Forms/Main.vb) / [ClipBoardViewer.vb](../Clases/ClipBoardViewer.vb)):

- 읽기를 재시도 방식으로 변경: 최대 5회, 150ms 간격으로 지연 렌더링과 클립보드 점유 경쟁을 모두 처리

- `WndProc`의 클립보드 액세스에 모두 예외 보호를 추가하여 일시적 실패가 더 이상 메시지 루프를 중단시키지 않음

- 처리 완료 후 클립보드 표시 쓰기 저장 실패 시 무시로 강등되며, 더 이상 충돌하지 않음

### 🔒 7z 압축 해제 보안 세부 사항

- 압축 해제 전 `l -ba -slt`로 전체 항목을 먼저 나열하고, PathGuard 검증을 거쳐 경로 탈출(Zip Slip)을 거부하며, 검증 통과 후에만 압축 해제 실행

- CLI 매개변수의 암호(`-p`)는 절대 로그에 기록하지 않음

- 하위 프로세스 stderr 비동기 읽기, 파이프 버퍼 교착 상태 회피

- 종료 코드 0/1(성공/경고)은 통과, 2(치명적, 예: 암호 오류)는 출력 꼬리를 포함하여 친화적인 오류로 발생

***

## [2.4.2] - 2026-08-19

### 🐛 수정: 다운로드 파일 실제 손상(사용자 실측 확인)

**증상**: 다운로드가 완료되고 파일 크기가 정확히 일치하지만 파일 내용이 손상되어 사용할 수 없었음. 로그에서 구버전(v2.4.1 포함)이 MetaMAC 체크섬 실패 후에도 "경고 후 통과" 방식으로 이름 바꾸기를 완료하여 손상된 파일이 그대로 디스크에 기록되었음이 확인됨.

**원인**(세 개의 독립 결함이 겹침):

1. **MetaMAC 청크 스케줄 알고리즘 오류**: v2.4.1은 "128K 시작 배수 증가, 8 MiB/1 MiB 이중 상한" 스케줄을 사용했으나, MEGA 공식 SDK `ChunkedHash::chunkfloor/chunkceil`의 실제 스케줄과 일치하지 않아 다수의 정상 파일이 mismatch로 오판되었음(하위 통과 로직의 빌미도 제공했음)
2. **mismatch 통과 정책**: 알고리즘 오류를 전제로, v2.4.1은 "mismatch 즉시 실패"를 "경고 기록 후 그대로 완료 및 이름 바꾸기"로 후퇴시켰음——체크섬이 유명무실해졌고, 실제 손상(예: URL 만료 후 403 기간의 빈 구멍 쓰기)이 그대로 통과되었음
3. **비정렬 이어받기로 인한 CTR 키 스트림 어긋남**: 연결 중단 시 best-effort flush가 16바이트 정렬에 못 미치는 진행률을 영속화했음; 재시도 시 `SeekToFileOffset`이 정수 블록 단위로만 키 스트림 위치를 지정할 수 있어, 어긋난 지점부터 **이후 모든 데이터 복호화가 어긋남**——이것이 "크기는 맞지만 내용이 손상됨"의 직접 원인임

**수정**:

| 위치                                        | 수정                                                                                                          |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `Criptografia.ComputeMegaFileMac`         | 청크 스케줄을 MEGA SDK 선형 경계로 변경: 128 KiB × i(i=1..8, 즉 128/256/384/512/640/768/896 KiB), 이후 1 MiB 고정; 이중 cap 대체 로직 삭제, 1회 계산 |
| `Criptografia.VerifyMegaMetaMac`          | 빈 파일은 바로 (0,0) 반환(MEGA 빈 파일 MetaMAC은 0임)                                                                        |
| `FileDownloader.downloadFile`             | MetaMAC 불일치는 오류 로그를 기록하지만 MEGA SDK 관대 정책에 따라 계속 완료(SDK도 과거 "MAC 꼬리 항목 누락"에 마찬가지로 관대함); 파일 크기 정밀 체크섬은 하드 게이트로 유지                             |
| `FileDownloader.FlushToDisk`              | 중단 flush 시 영속화 진행률을 16바이트 경계로 내림 정렬하여 비정렬 이어받기 지점을 근절(<16바이트 복호화된 데이터는 재시도 시 자동 재요청)                                                  |
| `ChunkDownloader_DoWork`                  | 이어받기 요청 전 시작점 정렬 검증: 16바이트 정렬이 아닌 이어받기 시작점은 chunk를 바로 중단하여 키 스트림 어긋남 방지                                                                |
| `DataPart.ValidateAndNormalize`           | 시작 시 구버전에 남은 비정렬 XML 진행률을 자동으로 16바이트 경계로 되돌림                                                                           |
| `FileDownloader.downloadFile`(v2.4.1에서 도입) | 유지: "파일 크기 일치 시 강제 완료" force-finish 제거; 실제 chunk가 모두 완료되어야만 완료로 판정; 120초 시간 초과 시 실패 보고 및 이어받기 지점 유지                                       |

### 📦 버전 번호

- Assembly / FileVersion → `2.4.2.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.2`

- `docs/version.xml` → `2.4.2.0`

***

## [2.4.1] - 2026-08-15

### 🐛 수정: 다운로드 완료 후 오류 표시(사용자 실측 확인)

**증상**: 파일이 100% 다운로드되면 바로 오류가 뜨고, `.part` 파일의 이름이 바뀌지 않음; 수동으로 `.part` 접미사를 제거하면 파일을 정상 사용할 수 있어 파일이 실제로는 완전히 다운로드되었음이 입증되었음.

**원인**: v2.2.0에서 도입한 MEGA MetaMAC 무결성 체크섬에 시스템적 오보가 존재:

1. **단일 파일 공개 링크는 반드시 오보**: 공개 링크의 FileKey는 16바이트(4 words, AES 키 자체만)이며 **MetaMAC을 포함하지 않음**; 그런데 `VerifyMegaMetaMac`은 최소 8 words(32바이트, nonce + MetaMAC 포함)를 요구하므로, 조건 미달 시 바로 False 반환 → 완료 경로에서 "Integrity check failed" 발생 → 상태 오류, 이름 바꾸기 미수행. 폴더 API가 반환한 32바이트 node key에만 진짜 MetaMAC이 포함되므로, 오보는 가장 흔한 단일 파일 링크 시나리오에 집중되었음
2. **8 words key의 경계 규칙 차이도 오보 가능**: MEGA 클라이언트 역사상 MAC 청크 경계 규칙에 버전 차이가 있으며, 불일치가 곧 파일 손상을 의미하지는 않음(크기 정밀 체크섬이 더 강한 증거임)

**수정**(2중 보호):

| 위치                               | 수정                                                                  |
| -------------------------------- | ------------------------------------------------------------------- |
| `Criptografia.VerifyMegaMetaMac` | 4 words 공개 링크 key는 "검증할 MetaMAC 없음"이라는 로그를 남기고 건너뜀(True 반환), 더 이상 실패로 오판하지 않음            |
| `FileDownloader.downloadFile`    | 8 words key의 MetaMAC 불일치는 로그 경고로 강등하고 계속 완료 및 이름 바꾸기 진행; 파일 크기 정밀 체크섬(불일치 시 여전히 오류 보고)은 주요 무결성 방어선으로 유지 |

### 📦 버전 번호

- Assembly / FileVersion → `2.4.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.1`

- `docs/version.xml` → `2.4.1.0`

***

## [2.4.0] - 2026-08-14

### 🐛 전면 버그 수정 - 21건 확인된 문제

v2.3.0 전체 프로젝트 코드를 파일별로 검사한 결과를 바탕으로, 정확한 코드 증거로 확인된 21건 버그를 수정했으며, 사용자 체감 오류, 교착 상태/리소스 누수, 동시성 결함, 예외 삼킴, 보안 부채 및 데드 코드를 포함합니다.

### 1. 사용자 체감 오류

| 수정                    | 설명                                                                                                                                            |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| 백그라운드 스레드 MessageBox로 다운로드 멈춤 | `FileDownloader.bgwDownloader_DoWork`의 Catch 블록이 더 이상 스레드 풀 스레드에서 `MessageBox.Show`를 호출하지 않으며, `ReportProgress(FileDownloadFailedRaiser)`를 통해 실패를 보고하고 UI 스레드에서 일괄 표시하도록 변경 |
| 종료 중 크로스 스레드 MsgBox 충돌     | `Main.vb` 3곳의 `BackgroundWorker.DoWork` 예외 분기가 더 이상 직접 `MsgBox`를 호출하지 않으며, `IsDisposed`/`IsHandleCreated`를 검사하고 `Invoke`로 UI 스레드에 복귀하는 `SafeShowError` 헬퍼 메서드 신규 추가      |
| 7z multipart 압축 해제 충돌     | `DescompresorController` 2곳의 `NotImplementedException`을 친화적인 메시지가 포함된 `NotSupportedException`으로 변경; `DescompresionFinalizada` 이벤트를 확장하여 오류 메시지를 전달하며, 사용자가 오류 상태에서 구체적인 원인을 볼 수 있음   |
| 대체 완료 시 MD5 체크섬/압축 해제 누락      | `Fichero.ActualizarDatosDescarga`의 대체 상태 수정이 더 이상 상태만 뒤집지 않으며, 완전한 `downloader_Completed` 흐름(MD5 체크섬과 자동 압축 해제 포함)을 호출하도록 변경하여 "완료로 표시되지만 무결성이 검증되지 않음" 방지                              |

### 2. 교착 상태 및 리소스 누수

| 수정                     | 설명                                                                                        |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| 뮤텍스 Try/Finally 누락으로 교착 상태 | `Main.AgregarPaquete`와 `bgwComprobarMaxConexiones` 2곳의 뮤텍스에 `Try/Finally` 추가, 중간 예외가 더 이상 영구 교착 상태를 유발하지 않음 |
| 다운로드 worker 미 Dispose    | `FileDownloader.Dispose`에서 `listDownloaders`의 모든 worker를 순회하며 해제하며, 더 이상 `CancelAsync`만 호출하지 않음            |
| bgArranque worker 누수   | `Fichero.Dispose`에 `bgArranque`(다운로드 시작 worker) 해제 신규 추가, 종료 중 시작 단계 중단 시 더 이상 누수 없음                           |
| ELCForm 300ms 바쁜 폴링      | `AutoResetEvent` 이벤트 기반으로 변경, 작업이 없을 때 CPU 소모 0, 작업이 있을 때 즉시 응답                                            |

### 3. 동시성 및 논리 결함

| 수정                   | 설명                                                                                  |
| -------------------- | ----------------------------------------------------------------------------------- |
| AJAX 응답 동시성 오염          | `StreamingLibraryModule._RespuestaAjax`를 `AsyncLocal(Of String)`으로 변경, 동시 HTTP 요청이 서로 응답을 덮어쓰지 않음 |
| FlushFinalBlock 예외 삼킴 | `ServerEncoderLinkHelper.Cipher` 복호화 경로의 빈 Catch를 `Log.WriteError`로 변경                   |

### 4. 예외 삼킴(실제 오류 은폐)

| 수정                 | 설명                                                                                        |
| ------------------ | ----------------------------------------------------------------------------------------- |
| FlushToDisk 디스크 오류 삼킴 | `FileDownloader.ChunkDownloader_DoWork`의 `FlushToDisk` 빈 Catch를 로그 기록으로 변경                     |
| 서버 오류 응답 읽기 실패 삼킴      | `Fichero.downloader_FileDownloadFailed` / `downloader_ChunkDownloadFailed` 2곳의 빈 Catch를 로그 기록으로 변경 |
| 압축 해제 취소 예외 삼킴           | `Main` 종료 흐름의 `RequestCancel` 빈 Catch를 로그 기록으로 변경                                                |

### 5. 보안 및 기술 부채

| 수정                        | 설명                                                                          |
| ------------------------- | --------------------------------------------------------------------------- |
| DPAPI entropy 하드코딩         | `Criptografia`의 DPAPI entropy를 어셈블리 식별자 SHA256 파생으로 변경, 기존 entropy로 구 데이터 복호화 유지   |
| ZIP 암호 하드코딩 "passZIP"       | `Fichero`의 ZIP 압축 해제 암호 암호화를 DPAPI로 변경, 복호화는 먼저 DPAPI 시도 후 구 AES로 대체하여 구 큐 파일과 호환                   |
| OptionalPassword 데드 필드      | `Cache.OptionalPassword` 필드 및 XML 쓰기 삭제(선언 후 한 번도 할당되지 않았고 읽히지도 않았음)                        |
| RandomNumberGenerator 미해제 | `ServerEncoderLinkHelper`의 `RandomNumberGenerator.Create()`를 `Using`으로 감싸 해제 |
| 로그 UTC 타임스탬프 누락               | `Log`의 모든 타임스탬프를 `DateTime.UtcNow`로 변경(`Z` 접미사 추가), 30일 로그 보존 정리 정책 신규 추가                   |

### 6. 데드 코드 정리

| 수정                 | 설명                                     |
| ------------------ | -------------------------------------- |
| Criptografia 주석 데드 코드 | 주석 처리된 `DecryptFile`과 `cipherData` 함수 삭제 |
| Conexion 데드 코드       | 주석 처리된 `GetAppID`와 호출 없는 `LeerNodo` 함수 삭제   |

### 📦 버전 번호

- Assembly / FileVersion → `2.4.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4`

- `docs/version.xml` → `2.4.0.0`

***

## [2.3.0] - 2026-08-13

### 🐛 안정성 수정

코드 리뷰를 바탕으로 충돌, 리소스 누수 및 잠재적 교착 상태 문제를 일괄 수정했습니다.

| 수정                | 설명                                                                                                                             |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| AES 암호화 실패 충돌        | `AES_EncryptString`이 암호화 예외 후에도 `Nothing`에 대해 Base64 변환을 수행하여 2차 예외 발생; 실패 시 빈 문자열 반환 및 `RijndaelManaged`/`CryptoStream`/`MemoryStream`을 `Using`으로 해제하도록 변경 |
| AES 복호화 잘림/손상 입력 충돌    | `AES_DecryptString`의 `Convert.FromBase64String`을 예외 처리 안으로 이동; `CopyTo`로 평문을 완전히 읽음(원래 단일 `Read`는 잘릴 수 있었음); 실패 시 빈 문자열 반환                              |
| 다운로드 항목 리소스 누수           | `Fichero.Dispose`를 빈 구현에서 `FileDownloader` 해제 후 비우기로 변경                                                                                |
| 잠재적 교착 상태              | `FileInfo.Size` setter의 `ReleaseMutex`를 `Try/Finally`에 배치, 루프 내 예외가 더 이상 영구 교착 상태를 유발하지 않음                                                         |
| 레지스트리 핸들 누수           | `RegisterInStartup`의 레지스트리 키를 `Try/Finally` + `Close()`로 해제                                                                        |
| 위험한 `Thread.Abort` | DLC 처리 30초 시간 초과 시 스레드를 강제로 종료하지 않고, 협력적 표시로 실패 처리한 뒤 worker가 자연 종료하도록 변경                                                                                    |

### 📦 버전 번호

- Assembly / FileVersion → `2.3.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.3`

- `docs/version.xml` → `2.3.0.0`

## [2.2.1] - 2026-08-09

### 🐛 다운로드 상태 수정

사용자가 보고한 다운로드 완료 상태 표시 문제 2건을 수정했습니다.

### Bug 1: 다중 파일 다운로드 완료(100%) 후 오류 표시

- **원인**: `FileDownloader.downloadFile()`의 `Finally` 블록이 `exc`가 비어 있지 않으면 `FileDownloadFailedRaiser`를 보고했음. 모든 청크가 이미 성공적으로 완료되었어도(`AllFinished = True`) 이전에 발생한 비치명적 예외가 여전히 실패 이벤트를 발생시켜 상태를 잘못 `Erroneo`로 설정했음.

- **수정**: `Finally` 블록에서 `AllFinished` 상태를 확인하고, 다운로드가 실제로 완료되었으면 `exc`를 지우며, 경고만 기록하고 실패를 보고하지 않음.

### Bug 2: 단일 파일 다운로드 완료(100%) 후에도 계속 "다운로드 중" 표시

- **원인**: `Completed` 이벤트는 `bgwDownloader_RunWorkerCompleted`에서만 발생하므로, 대기 루프가 경쟁 조건으로 빠져나오지 못하면 `Completed`가 절대 발생하지 않고 상태가 `Descargando`에 머물렀음.

- **수정**: 3중 보호

  1. **이벤트 계층**: `FileDownloadSucceeded` 핸들러 신규 추가, 파일 검증과 이름 바꾸기 성공 후 즉시 `Completado` 상태 설정
  2. **루프 계층**: 대기 루프에 60초 시간 초과 검사 추가, 디스크 파일 크기가 일치하면 강제 완료; 120초 하드 시간 초과로 교착 상태 방지
  3. **타이머 계층**: `ActualizarDatosDescarga`에 대체 검사 추가, 진행률 100%이며 `AllFinished`인 경우 자동으로 상태 정정

### 📦 버전 번호

- Assembly / FileVersion → `2.2.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2.1`

- `docs/version.xml` → `2.2.1.0`

***

## [2.2.0] - 2026-07-20

### 보안 강화 및 다운로드 무결성

심층 정적 감사 결론을 바탕으로 경로 보안(P0), 다운로드 무결성(P1) 및 일괄 안정성/릴리스 현대화(P2/P3) 수정을 완료했습니다.

### 🔒 경로 보안(P0)

- 통합 `PathGuard`: 원격 파일 이름/디렉터리 이름, 압축 해제 항목, 삭제 및 쓰기 모두 canonical 다운로드 루트 디렉터리 안으로 제한

- Zip Slip 수정: 압축 해제 전 전체 항목을 검증하고, `../`, 절대 경로, 장치 이름 등 탈출을 거부

- MEGA 폴더 경로 이어 붙이기 및 작업 삭제 범위 초과 위험 수정

### 📦 다운로드 무결성 및 안정성(P1)

- 다운로드 완료 전 **MEGA MetaMAC** 검증; 실패 시 최종 파일로 이름을 바꾸지 않음

- HTTP Range: Partial Content / Content-Range 검증; Range를 무시하는 오류 응답 거부

- 조기 EOF를 실패로 처리; CTR counter에 Int64 seek 사용, 대용량 오프셋 위험 수정

- 이어받기 메타데이터 검증, `.part` 누락 시 "가짜 완료" 방지

- 설정과 다운로드 큐 원자적 저장(`AtomicFile`); HTTP 기본 시간 초과; 로그 마스킹

- 원격 Web: Stop/Play/AddLink를 POST + CSRF로 변경; Streaming 미디어 URL을 loopback으로 고정

- 압축 해제 협력적 취소(`Thread.Abort` 제거), 압축 해제 결과 성공/실패 분리, 리소스 할당량

- 종료 순서: 먼저 Web 중지 → worker/압축 해제 취소 → 다운로드 중지 → 저장

### ✨ 경험 및 엔지니어링(P2/P3)

- 설정 모델 계층 상한(Buffer/연결 수/속도), 디스크 공간 사전 검사, 파일 이름 충돌 및 진행률 0 나누기 보호

- 언어: 내장 패키지와 사용자 사용자 지정을 분리, 키 누락 시 en-US 대체

- 단일 인스턴스 IPC를 행 단위로 기록, 링크 매개변수 붙음 방지; 테마 Auto가 시스템을 따라 실시간 변경

- 프로덕션 xUnit 의존성과 MPRESS Release 후처리 제거; DPI PerMonitorV2

- 버전 비교 정규화; DLC 진입점을 discontinued로 표시(ELC 유지)

### 📦 버전 번호

- Assembly / FileVersion → `2.2.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2`

- `docs/version.xml` → `2.2.0.0`

***

## [2.1.0] - 2026-07-19

### 테마 개선 - 다크 모드 사용성 수정

v2.0 테마 프레임워크를 바탕으로 다크 모드의 주 목록, 진행률 표시줄, 버튼 테두리, 마우스 오른쪽 클릭 메뉴 등 핵심 표시 문제를 수정하여 Dark 테마를 실제로 사용할 수 있게 했습니다.

### 🐛 수정

- **주 다운로드 목록 얼룩무늬**: `FormatRow`가 더 이상 `White`/`Honeydew`를 하드코딩하지 않으며, `ThemeManager`의 `Back`/`AltBack`으로 변경

- **진행률 표시줄 색상**: `BarRenderer`가 더 이상 Azure/SpringGreen을 사용하지 않으며, 테마 토큰(`ProgressBack`/`ProgressFill` 등)으로 변경

- **상태 전경색**: 오류/완료 행에 `ErrorFore`/`SuccessFore` 사용(다크 모드에서는 더 밝은 빨강/초록)

- **설정 저장 후 즉시 스킨 적용**: Configuration 저장 후 테마가 `Main.ApplyCurrentTheme()`을 호출하여 재시작 불필요

- **버튼 흰색 테두리**: `FlatStyle.Standard`의 시스템 3D 하이라이트가 다크 모드에서 흰색 테두리로 보였음; `FlatStyle.Flat` + 테마 `Border`/`ButtonHover`/`ButtonPressed`로 변경

- **GroupBox / TabPage**: Flat 테두리와 `UseVisualStyleBackColor = False`로 시스템 밝은색 테두리 감소

- **ELC 계정 테이블**: Azure/Snow/SeaShell 하드코딩 제거; 빈 목록 알림은 테마 전경색 사용

- **마우스 오른쪽 클릭 메뉴**: 리플렉션으로 Form의 `ContextMenuStrip` 테마화; `ToolStripDropDownBackground` 등 `ThemeColorTable` 속성 보완

- **테마 미적용 창**: Stegano 마법사, SplashScreen, Cerrando가 Load 시 `ApplyTheme` 적용

### ✨ 개선

- `ThemeManager.GetColor(key)` 공용 색상 API

- 신규 의미/상호작용 토큰: `ErrorFore`, `SuccessFore`, `Progress*`, `ButtonHover`, `ButtonPressed`

- `ToolStripBorder`가 `ToolBorder` 토큰을 올바르게 사용

### 📦 버전 번호

- Assembly / FileVersion → `2.1.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.1`

- `docs/version.xml` → `2.1.0.0`

***

## [2.0.0] - 2026-07-13

### 주요 버전 - 보안 강화 + 코드 정리 + 다크 테마

v1.9의 링크 형식 수정을 바탕으로 4단계 총 60여 건 수정을 추가로 완료하여 보안, 안정성 및 사용성을 크게 향상시켰습니다. 본 버전에서 처음으로 다크/라이트 테마 전환을 도입했습니다.

### ✨ 신규 추가

- **다크/라이트 테마 전환**:

  - `ThemeModeType` 열거형(Auto/Light/Dark) 신규 추가, 기본값 Auto로 시스템 추적([`Clases/ConfiguracionUI.vb`](../Clases/ConfiguracionUI.vb))

  - `ThemeManager` 클래스 신규 추가, 레지스트리 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` 읽기로 시스템 다크/라이트 감지([`Clases/ThemeManager.vb`](../Clases/ThemeManager.vb))

  - 사용자 지정 `ThemeColorTable` + `ToolStripProfessionalRenderer` 렌더러, 30여 개 ToolStrip 그라데이션/테두리/선택 색상 속성 재정의

  - 모든 컨트롤에 테마를 재귀 적용, 주 창의 `BrightIdeasSoftware.TreeListView`(다운로드 목록), StatusStrip, ContextMenuStrip, TableLayoutPanel, DataGridView, ListView, TreeView, ProgressBar 등 포함

  - 9개 하위 창이 Load 이벤트에서 테마 적용: Credits, AddLinks, ELCForm, EncodeLinksForm, PropiedadesDescarga, StreamingForm, Descompresor, PantallaMsg, Configuration

  - 10개 언어 파일에 `Theme` / `Theme_Auto` / `Theme_Light` / `Theme_Dark` 번역 키 추가

- **작성자 정보**: Credits 창에 "Yingxue - Revival maintainer (v2.0+)" 추가

- **업데이트 확인**: 본 GitHub 저장소로 리디렉션

### 🐛 수정

**P0 심각한 보안 취약점**:

- 빈 암호 우회 검증 로직 수정

- 프록시 자격 증명이 WebProxy에 실제로 할당되지 않던 문제 수정

- TLS 1.2만 활성화(TLS 1.0/1.1 제거, 최신 보안 표준 준수)

- Web/스트리밍 서버를 `127.0.0.1`에 바인딩(원래 `0.0.0.0`은 전체 네트워크에 노출되었음)

- 암호 해시를 UTF-8 인코딩으로 통일

- 뮤텍스 작업을 모두 Try/Finally로 감싸 교착 상태 방지

- 암호화 코드의 빈 Catch 블록을 로그 기록으로 교체

- `ApagarPC` / `MaxConexionesGuardadas` 설정이 올바르게 영속화되지 않던 문제 수정

- 다운로더 `NullReferenceException` 충돌 수정(변수 이름 불일치 `exc` vs `ex`)

**P1 리소스 누수**:

- 7곳 ToolTip 리소스 누수 수정(`ELCAccountControl` / `AddLinks` / `SteganoWizardSave`, MouseHover마다 생성 후 해제하지 않았음)

- `SteganoManager`의 `Image.FromFile` 원본 파일 잠금 + FileStream 미 Dispose 수정

- `Main.vb` 7곳의 `Image.FromStream(stream)`에서 stream 조기 닫기 수정, `LoadEmbeddedImage` 헬퍼 메서드 신규 추가

- `WebInterfaceModule` StreamReader/StreamWriter 미 Using 수정(템플릿 불러오기 + response.Body 쓰기, 후자는 `leaveOpen:=True` 사용)

- `StreamingLibraryManager` `CompressString` / `UnCompressString` 미 Using 수정(중첩 Using 블록)

- `MegaURIProtocol` 레지스트리 작업 Finally 해제 누락 + 중간 변수 덮어쓰기로 인한 핸들 누수 수정

- `Main.vb` `clipChange` 닫기 순서 오류 수정(Uninstall이 DestroyHandle보다 먼저 수행되어야 함)

- `Main.vb` `EsperarParadaDescargasYWorkers`에서 `bgwDescompresorCompleted` 검사 누락 수정

- `StreamingLibraryModule` `Case "Delete"`에 `Return True` 누락으로 다음 분기로 관통하던 문제 수정

- `StreamingLibraryModule` `UsuarioLogueado`가 시간 초과 후 session을 지우지 않아 로그인 상태가 영구 유지되던 문제 수정

- `ELCAccountControl` `CellClick`이 `e.RowIndex`를 검증하지 않아 헤더 클릭 시 충돌하던 문제 수정

- `StreamingHelper` `Keys.Count / 2` 부동소수점 나눗셈 수정, 정수 나눗셈 `\ 2` 사용

**P2 프로토콜 현대화**:

- `%SEQ%` / `%ID%` 일련번호가 원래 `DateTime.Now.Millisecond`의 ticks(범위 0-999, 동시 요청 시 중복)를 사용했음; `Interlocked.Increment` 프로세스 내 증가로 변경

- `MegaFolderHelper.vb`에서 `http://mega.co.nz/#N!` → `https://mega.nz/#N!`

**P2 코드 품질**:

- `Paquete.vb` / `Configuracion.vb`가 설정 XML 비교에 `GetHashCode` 사용(일관성 보장 불가), 직접 `OuterXml` 문자열 비교로 변경

- `MegaFolderHelper.vb` 2곳의 변수 `ex`(Regex)→ `rx`(`Catch ex` 혼동 방지)

- `ThrottledStream.vb` 변수 이름 `int`(VB.NET 키워드)→ `bytesRead`

- `Clases/Mutex.vb` 클래스 이름이 `System.Threading.Mutex`를 가림, 주석 설명 + 별칭 방안 제공

- `StreamingModule.ClientConnected`, `FileDownloader` Range 헤더 리플렉션 필요성 주석 설명 추가

- `LibraryElement.ToJSON` 수동 JSON 이어 붙이기 제한 주석 설명 추가

### 🗑️ 삭제

- **4개 Crypter**: `EncrypterMega.vb`, `MegaCrypter.vb`, `Youpaste.vb`, `LinkCrypter.vb`(API 전부 서비스 종료)

- **3개 MovieInfo**: `Allocine.vb`, `Filmaffinity.vb`, `IMDB.vb`(API 전부 변경)

- **링크 보조**: `DLCHelper.vb`, `Linkdecrypter.vb`, `LinkProtectors.vb`, `Serializer.vb`, `ClipboardChangeNotifier.vb`

- **MegaUploader 메뉴**: "Get MegaUploader" 메뉴 항목 제거

- **goo.gl 단축 링크**: 14개 Google 단축 링크를 모두 GitHub 직접 링크로 교체

- **Ping 보고**: 원작자 서버로의 사용자/버전 정보 보고 제거(개인정보 보호)

- 총 11개 `.vb` 파일 삭제 + 모든 관련 참조 정리

### ⚠️ 알려진 문제

- `Thread.Abort()` 위험한 사용(3곳, Main.vb / DescompresorController)

- 크로스 스레드 MsgBox가 창이 이미 닫혔는지 검사하지 않음(3곳)

- `MegaFolderHelper.FillFolderStructure` 재귀에 KeyNotFound 보호 없음

- `ELCForm` 무한 루프 300ms 폴링

- `ServerEncoderLinkHelper` RandomNumberGenerator 미 Dispose

- `FileDownloader.FlushToDisk` FileStream 비동기 해제

### 📦 빌드 산출물

- `MegaDownloader.exe` 주 프로그램

- 의존 DLL: `BouncyCastle.Crypto.dll`, `Newtonsoft.Json.dll`, `SharpCompress.dll`, `ObjectListView.dll`, `HttpServer.dll`, `Fadd.dll`, `F5Lib.dll`, `xunit.dll`

***

## [1.9.1] - 2026-07-05

### 🐛 수정

- **다운로더 충돌**: [`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb) 681-683행 변수 이름 불일치로 인한 `NullReferenceException` 수정. MEGA 서버가 502 게이트웨이 오류 등 예외를 반환할 때 catch 블록이 이미 비워진 `exc` 지역 변수를 잘못 참조했음(올바른 것은 `ex`), 실제 예외를 가리고 전체 다운로드 흐름을 중단시켰음.

***

## [1.9.0] - 2026-07-05

### MegaDownloader 부활 계획 첫 공개 릴리스 버전

MegaDownloader v1.8 디컴파일 소스를 기반으로 수정 및 리팩터링을 수행했으며, 핵심 목표는 MEGA 신버전 링크 형식 지원 복원입니다.

### ✨ 신규 추가

- **URL 파싱**: [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb)의 `patternHTTPURI`에 정규식 4건 신규 추가, 다음 신버전 MEGA 링크 식별 지원:

  - `https://mega.nz/file/<FileID>#<FileKey>`

  - `https://mega.nz/folder/<FolderID>#<FolderKey>`

  - `https://mega.co.nz/file/<FileID>#<FileKey>`

  - `https://mega.co.nz/folder/<FolderID>#<FolderKey>`

- **폴더 식별**: `IsMegaFolder` 메서드 동기 업데이트

- **TLS 1.2/1.3**: [`Clases/Conexion.vb`](../Clases/Conexion.vb)에서 `Tls12 | Tls11 | Tls` 프로토콜 명시적 활성화

- 본 저장소의 [README.md](README.md), [CONTRIBUTING.md](CONTRIBUTING.md), [CHANGELOG.md](CHANGELOG.md), `.gitignore` 등 개발자 문서 추가

### 🐛 수정

- 클립보드에서 신버전 MEGA 링크 복사 시 식별되지 않던 문제 수정

- 브라우저에서 신버전 MEGA 링크를 주 창으로 끌어다 놓아도 동작하지 않던 문제 수정

- 신버전 폴더 링크를 하위 파일 목록으로 파싱할 수 없던 문제 수정

- **폴더 다운로드 시 Base64 디코딩 오류 수정**: `mega.nz/folder/` 링크에 여러 사용자가 공유한 파일이 포함된 경우, MEGA API가 반환하는 `fileN.k` 필드 형식이 `handle1:key1/handle2:key2[/handle3:key3]`(`/`로 구분된 여러 `handle:key` 쌍)임. 원래 코드 `fileN.k.Substring(fileN.k.IndexOf(":") + 1)`은 첫 `:` 뒤의 모든 내용(`/handle2:key2` 포함)을 key로 간주하여 `Convert.FromBase64String`에서 FormatException 발생. 수정 방안: `ExtractKeyFromK` 헬퍼 함수 신규 추가.

### 🔄 변경

- `TargetFrameworkVersion`은 `v4.8` 유지(원래 v1.8에서 이미 4.8로 업그레이드되었음)

- 저장소 LICENSE는 MIT 프로토콜 유지, 부활 계획 저작권 표시 보완

### ⚠️ 알려진 문제

- EncrypterMe.ga는 공식 API 서비스(`http://encrypterme.ga/api`)가 서비스 종료되어 현재 이런 종류의 링크를 파싱할 수 없음

- 일부 goo.gl 단축 링크는 Google이 해당 서비스를 종료하여 점프할 수 없음

- 간체 중국어 언어 팩은 일부 항목의 번역 보완이 필요함

### 📦 빌드 산출물

- `MegaDownloader.exe` 주 프로그램

- 의존 DLL: `BouncyCastle.Crypto.dll`, `Newtonsoft.Json.dll`, `SharpCompress.dll`, `ObjectListView.dll`, `HttpServer.dll`, `Fadd.dll`, `F5Lib.dll`, `xunit.dll`

***

## [1.8.0] - 원본 (디컴파일 소스)

부활 계획이 기반한 원래 버전이며, 본 저장소는 디컴파일을 통해 소스를 얻어 수정 출발점으로 삼았습니다.

### 주요 특성

- 다중 스레드 동시 다운로드

- MEGA 폴더 재귀 파싱

- 암호화 링크(`enc`/`enc2`/`fenc`/`fenc2`/`elc`) 지원

- 타사 Crypter 통합 (MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga)

- VLC 스트리밍 이어보기

- 내장 HttpServer Web 관리 화면

- SharpCompress 자동 압축 해제

- 다국어 화면 (10종)

- Stegano 스테가노그래피

- 자동 업데이트 확인

***

## 버전 번호 설명

- 주 버전 번호: 주요 기능 변경 또는 하위 호환되지 않는 수정

- 부 버전 번호: 신규 기능 추가, 하위 호환

- 수정 번호: 버그 수정, 하위 호환
