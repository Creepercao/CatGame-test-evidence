*** Settings ***
Documentation    Explicit QA near-threshold fixture; actual passive earning changes the room on the ordinary APK.
Library    S1Android.py    ${SERVER}    ${ADB}    15037    emulator-5564    10    ${OUTPUT DIR}    false    18326
Suite Setup    Open Appium
Suite Teardown    Close Appium

*** Test Cases ***
S1-GROWTH-01 Passive Earning Changes Room And Persists
    Prepare Explicit Near Threshold Fixture
    Start Cat Game
    Wait For Visual    menu    menu.png    timeout=45
    Tap    1320    372
    Wait For Visual    two-cats    loaded.png    timeout=20
    Wait For Room Pixels    Starter    starter.png
    Wait For Room Pixels    Companions    companions.png
    Assert Saved Theme Progress    progress-after.json
    Force Stop Cat Game
    Sleep    2s
    Start Cat Game
    Wait For Visual    menu    restart-menu.png    timeout=45
    Tap    1320    372
    Wait For Visual    offline    restart-offline.png    timeout=20
    Tap    800    519
    Wait For Visual    two-cats    restart-playable.png    timeout=20
    Wait For Room Pixels    Companions    restart-companions.png

S1-TOUCH-01 Standard    
    Check Native Touch    1600    1320    0    standard
S1-TOUCH-02 Wide
    Check Native Touch    2000    1650    200    wide
S1-TOUCH-03 Compact
    Check Native Touch    1200    990    -200    compact

*** Keywords ***
Check Native Touch
    [Arguments]    ${width}    ${menu_x}    ${modal_offset}    ${tag}
    Force Stop Cat Game
    Set Display Width    ${width}
    Start Cat Game
    ${menu_offset}=    Evaluate    int($menu_x) - 1320
    Wait For Visual    menu    ${tag}-menu.png    timeout=45    width=${width}    offset_x=${menu_offset}
    Tap    ${menu_x}    372
    Wait For Visual    offline    ${tag}-offline.png    width=${width}    offset_x=${modal_offset}
    ${center}=    Evaluate    int($width) // 2
    Tap    ${center}    519
    Wait For Two Cats And Dismiss Pending Cg    ${width}    ${tag}
    Wait For Room Pixels    Companions    ${tag}-companions.png    ${width}
    Pet Visible Milk And Verify Saved Click    ${tag}
    Force Stop Cat Game
    Restore Display Size
