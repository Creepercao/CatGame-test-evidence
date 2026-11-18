*** Settings ***
Documentation    Install ordinary 1.1.0 over the naturally played ordinary 1.0.2, preserve bytes and continue via Appium.
Library          F1ArtAndroid.py    ${SERVER}    ${ADB}    15037    emulator-5564    10    ${OUTPUT DIR}    false    8206
Suite Setup      Open Appium
Suite Teardown   Close Appium

*** Test Cases ***
F1-UPGRADE-01 Ordinary APK Keeps The Actual Old Save And Loads Two Cats
    Upgrade Normal Apk Without Changing Save    ${APK}
    Start Cat Game
    Wait For Visual    menu    01-new-menu.png    timeout=45
    Tap    1320    372
    Wait For Visual    offline    02-offline-summary.png
    Tap    800    519
    Wait For Visual    two-cats    03-loaded-two-cats.png
    Upgraded State Retains Owned Cats And Clicks
