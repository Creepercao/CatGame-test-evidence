*** Settings ***
Documentation     Android ARM64 Player smoke using Appium UiAutomator2 on a fresh, dedicated Android user.
Library           AndroidGame.py    ${SERVER}    ${ADB}    ${ADB_PORT}    ${SERIAL}    ${QA_USER}    ${OUTPUT DIR}
Suite Setup       Open Appium
Suite Teardown    Close Appium

*** Variables ***
${SERVER}         http://127.0.0.1:4723
${ADB_PORT}       15037
${UI_TRANSITION_TIMEOUT}    12

*** Test Cases ***
ANDROID-SMOKE-01 Purchase Survives Home And Cold Restart
    [Documentation]    Fresh QA save only. No save injection. The second cat is purchased with passive income.
    [Tags]    android    appium    smoke    save-lifecycle
    Force Stop Cat Game
    Start Cat Game
    Wait For Visual    menu    01-main-menu.png    timeout=${UI_TRANSITION_TIMEOUT}
    ${tree}=    Capture UI Hierarchy    01-native-tree.xml
    Should Contain    ${tree}    com.catgamestudio.catgame
    Tap    1320    457
    Wait For Visual    intro    02-new-game-intro.png    timeout=${UI_TRANSITION_TIMEOUT}
    Tap    1280    839
    Wait For Visual    one-cat    03-one-cat.png    timeout=${UI_TRANSITION_TIMEOUT}
    ${before}=    Cat Game PID
    Should Not Be Empty    ${before}
    Press Key    3
    Wait Until Keyword Succeeds    5s    300ms    Cat Game Is Backgrounded
    Capture    04-home.png
    Start Cat Game
    Wait For Visual    one-cat    04-warm-resume.png    timeout=${UI_TRANSITION_TIMEOUT}
    ${warm}=    Cat Game PID
    Should Be Equal    ${before}    ${warm}
    Wait For Affordable Milk
    Buy And Press Home
    Wait Until Keyword Succeeds    5s    300ms    Cat Game Is Backgrounded
    Force Stop Cat Game
    Sleep    2s    Ensure a positive elapsed time for the offline summary.
    Start Cat Game
    Wait For Visual    menu    07-relaunch-menu.png    timeout=${UI_TRANSITION_TIMEOUT}
    ${after}=    Cat Game PID
    Should Not Be Empty    ${after}
    Should Not Be Equal    ${before}    ${after}
    Tap    1320    372
    Wait For Visual    offline    08-offline-panel.png    timeout=${UI_TRANSITION_TIMEOUT}
    Tap    800    519
    Wait For Visual    two-cats    09-two-cats-restored.png    timeout=${UI_TRANSITION_TIMEOUT}
