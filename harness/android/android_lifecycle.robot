*** Settings ***
Documentation     Lock/unlock with Appium. Requires the existing two-cat save on the dedicated Android QA user.
Library           AndroidGame.py    ${SERVER}    ${ADB}    ${ADB_PORT}    ${SERIAL}    ${QA_USER}    ${OUTPUT DIR}
Suite Setup       Open Appium
Suite Teardown    Close Appium
Test Teardown     Unlock Screen

*** Variables ***
${SERVER}         http://127.0.0.1:4723
${ADB_PORT}       15037

*** Test Cases ***
ANDROID-LIFECYCLE-01 Lock Screen And Resume Touch Input
    [Tags]    android    appium    save-lifecycle
    Force Stop Cat Game
    Sleep    2s    Ensure a positive elapsed time for the offline summary.
    Start Cat Game
    Wait For Visual    menu    01-menu.png
    Tap    1320    372
    Wait For Visual    offline    02-offline.png
    Tap    800    519
    Wait For Visual    two-cats    03-before-lock.png
    ${before}=    Cat Game PID
    Should Not Be Empty    ${before}
    Lock Screen
    Wait Until Keyword Succeeds    8s    500ms    Screen Should Be Locked    true
    Unlock Screen
    Wait Until Keyword Succeeds    8s    500ms    Screen Should Be Locked    false
    Wait For Visual    two-cats    04-unlocked.png
    ${resumed}=    Cat Game PID
    Should Be Equal    ${resumed}    ${before}
    Tap    330    43
    Wait For Visual    pause    05-touch-after-unlock.png
    Press Key    4
    Wait For Visual    two-cats    06-back-to-game.png
