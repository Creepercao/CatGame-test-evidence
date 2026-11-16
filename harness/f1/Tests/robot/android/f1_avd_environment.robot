*** Settings ***
Documentation    Recover the specifically observed Android System UI ANR once; not a game test pass.
Library          F1ArtAndroid.py    ${SERVER}    ${ADB}    15037    emulator-5564    10    ${OUTPUT DIR}    false    8206
Suite Setup      Open Appium
Suite Teardown   Close Appium

*** Test Cases ***
F1-ENV-01 Record And Wait For The Observed System UI Dialog
    Dismiss One Avd System Ui Anr    ${REFERENCE}
