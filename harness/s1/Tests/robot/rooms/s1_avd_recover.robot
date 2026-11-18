*** Settings ***
Library    ../android/F1ArtAndroid.py    ${SERVER}    ${ADB}    ${ADB_PORT}    ${SERIAL}    ${QA_USER}    ${OUTPUT DIR}    false    ${SYSTEM_PORT}
Suite Setup    Open Appium
Suite Teardown    Close Appium

*** Test Cases ***
Wait Once On Explicitly Reviewed AVD System UI ANR
    Dismiss One Avd System Ui Anr    ${REFERENCE}
