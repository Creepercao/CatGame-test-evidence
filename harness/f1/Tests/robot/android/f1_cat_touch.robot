*** Settings ***
Documentation    Real Appium touches on the displayed Milk body at three aspect ratios; read saved click counts after HOME.
Library          F1ArtAndroid.py    ${SERVER}    ${ADB}    15037    emulator-5564    10    ${OUTPUT DIR}    false    ${SYSTEM_PORT}
Suite Setup      Open Appium
Suite Teardown   Close Appium
Test Template    Check Grounded Cat Touch
Test Teardown    Stop And Restore Display

*** Variables ***
${SYSTEM_PORT}    8206

*** Test Cases ***
F1-TOUCH-01 16 By 9    1600    1320    0       standard
F1-TOUCH-02 20 By 9    2000    1650    200     wide
F1-TOUCH-03 4 By 3     1200    990     -200    compact

*** Keywords ***
Check Grounded Cat Touch
    [Arguments]    ${width}    ${menu_x}    ${modal_offset}    ${tag}
    Force Stop Cat Game
    Set Display Width    ${width}
    Start Cat Game
    ${menu_offset}=    Evaluate    int($menu_x) - 1320
    Wait For Visual    menu    ${tag}-menu.png    width=${width}    offset_x=${menu_offset}    handle_system_hint=true
    Tap    ${menu_x}    372
    Wait For Visual    offline    ${tag}-offline.png    width=${width}    offset_x=${modal_offset}
    ${center}=    Evaluate    int($width) // 2
    Tap    ${center}    519
    Wait For Two Cats And Dismiss Pending Cg    ${width}    ${tag}
    Pet Visible Milk And Verify Saved Click    ${tag}

Stop And Restore Display
    Force Stop Cat Game
    Restore Display Size
