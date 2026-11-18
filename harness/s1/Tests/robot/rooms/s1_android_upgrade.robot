*** Settings ***
Documentation    Actual natural 1.1.0 save upgrades to ordinary 1.2.0 before any fixture is introduced.
Library    S1Android.py    ${SERVER}    ${ADB}    15037    emulator-5564    10    ${OUTPUT DIR}    false    18326
Suite Setup    Open Appium
Suite Teardown    Close Appium

*** Test Cases ***
S1-UPGRADE-01 Old Natural Save Survives Ordinary APK Upgrade
    Upgrade S1 Preserving Natural Save    ${APK}
    Start Cat Game
    Wait For Visual    menu    new-menu.png    timeout=45
    Tap    1320    372
    Wait For Visual    offline    offline.png    timeout=20
    Tap    800    519
    Wait For Two Cats And Dismiss Pending Cg    1600    loaded
    Wait For Room Pixels    Starter    starter.png
    Upgraded State Retains Owned Cats And Clicks
