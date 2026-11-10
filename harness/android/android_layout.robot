*** Settings ***
Documentation     Two extra landscape shapes. Requires the two-cat QA save from android_smoke.robot.
Library           AndroidGame.py    ${SERVER}    ${ADB}    ${ADB_PORT}    ${SERIAL}    ${QA_USER}    ${OUTPUT DIR}
Suite Setup       Open Appium
Suite Teardown    Close Appium
Test Template     Check Landscape Layout
Test Teardown     Restore Display Size

*** Variables ***
${SERVER}         http://127.0.0.1:4723
${ADB_PORT}       15037

*** Test Cases ***
ANDROID-LAYOUT-01 20 By 9 Landscape    2000    1650    1000    200    705    wide
ANDROID-LAYOUT-02 4 By 3 Landscape     1200    990     600     -200   305    compact

*** Keywords ***
Check Landscape Layout
    [Arguments]    ${width}    ${menu_x}    ${center}    ${offset}    ${nav_x}    ${tag}
    Force Stop Cat Game
    Set Display Width    ${width}
    Start Cat Game
    Wait For Visual    menu    ${tag}-01-menu.png    width=${width}    offset_x=${{int($menu_x) - 1320}}
    Tap    ${menu_x}    372
    Wait For Visual    offline    ${tag}-02-offline.png    width=${width}    offset_x=${offset}
    Tap    ${center}    519
    Wait For Visual    two-cats    ${tag}-03-shop.png    width=${width}
    Swipe    190    675    190    330
    Sleep    500ms    Wait for scroll deceleration before capturing the list.
    Wait For Visual    two-cats    ${tag}-04-scrolled.png    width=${width}
    Shop Scroll Should Move    ${tag}-03-shop.png    ${tag}-04-scrolled.png
    Press Key    4
    Wait For Visual    pause    ${tag}-05-pause.png    width=${width}    offset_x=${offset}
    Tap    ${nav_x}    402
    Sleep    350ms    Wait for the achievements page to render.
    Capture    ${tag}-06-achievements.png
    Press Key    4
    Wait For Visual    two-cats    ${tag}-07-resumed.png    width=${width}
