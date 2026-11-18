*** Settings ***
Documentation     Regression for the release APK room clear-color strip at native phone resolution.
Library           PhoneGame.py    http://127.0.0.1:4723    ${ADB}    5037    ${SERIAL}    0    ${OUTPUT DIR}
Library           check_room_coverage.py
Suite Setup       Open Appium
Suite Teardown    Close Appium

*** Test Cases ***
PHONE-VIEWPORT-01 Room Covers Native Phone And Preserves Save
    Force Stop Cat Game
    Sleep    2s
    Start Cat Game
    Wait For Visual    menu    01-menu.png
    Tap UI    menu    1320    372
    Wait For Visual    offline    02-offline.png
    Tap UI    center    800    519
    Complete Pending Memories    viewport-entry
    Wait For Visual    three-cats    03-room.png
    Room Right Edge Should Be Covered    ${OUTPUT DIR}/03-room.png
    Saved Cat Count Should Be    3    04-save.json
