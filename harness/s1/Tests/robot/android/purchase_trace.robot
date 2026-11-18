*** Settings ***
Documentation    Diagnostic matrix on a separate opt-in APK; unacknowledged input remains a finding, not a passing purchase.
Library          Process

*** Variables ***
${PYTHON}        PYTHON_ROOT/python.exe
${QA_USER}       10
${HOME_POLICY}   raw
${DISABLE_WINDOW_ANIMATION}    false

*** Test Cases ***
ANDROID-INPUT-TRACE-01 Distinguish Input Acknowledgement From Save Durability
    [Documentation]    60/6 FPS x short/350ms x foreground/Home. Every accepted purchase must survive cold restart; all input failures stay in results.json.
    [Tags]    android    appium    diagnostic    purchase    save-lifecycle
    ${result}=    Run Process    ${PYTHON}    ${CURDIR}/trace_purchase.py
    ...    --adb    ${ADB}    --user    ${QA_USER}    --fixture    ${FIXTURE}
    ...    --output    ${OUTPUT DIR}/matrix
    ...    --home-policy    ${HOME_POLICY}
    ...    --disable-window-animation    ${DISABLE_WINDOW_ANIMATION}
    ...    stdout=${OUTPUT DIR}/matrix-console.txt    stderr=${OUTPUT DIR}/matrix-errors.txt
    ...    timeout=25 minutes    on_timeout=terminate
    Should Be Equal As Integers    ${result.rc}    0
