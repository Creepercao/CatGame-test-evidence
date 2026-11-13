*** Settings ***
Documentation     Room coverage at three landscape ratios in the isolated AVD test user.
Library           AvdRoomGame.py    http://127.0.0.1:4723    ${ADB}    5037    ${SERIAL}    ${QA_USER}    ${OUTPUT DIR}
Library           check_room_coverage.py
Suite Setup       Open Appium
Suite Teardown    Close Appium
Test Template     Check Room Coverage
Test Teardown     Restore Display Size

*** Test Cases ***
ROOM-VIEWPORT-01 16 By 9    1600    1320    0
ROOM-VIEWPORT-02 20 By 9    2000    1650    330
ROOM-VIEWPORT-03 4 By 3     1200    990     -330

*** Keywords ***
Check Room Coverage
    [Arguments]    ${width}    ${menu_x}    ${menu_offset}
    Force Stop Cat Game
    Restore Room Fixture    ${FIXTURE}    ${width}-fixture.json
    Set Display Width    ${width}
    Start Cat Game
    Wait For Visual    menu    ${width}-01-menu.png    width=${width}    offset_x=${menu_offset}
    Tap    ${menu_x}    372
    Wait For Visual    two-cats    ${width}-02-room.png    width=${width}
    Room Right Edge Should Be Covered    ${OUTPUT DIR}/${width}-02-room.png
