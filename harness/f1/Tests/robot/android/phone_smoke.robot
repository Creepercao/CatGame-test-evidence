*** Settings ***
Documentation     Release APK smoke at native 2712x1220 on the authorized Android phone.
Library           PhoneGame.py    http://127.0.0.1:4723    ${ADB}    5037    ${SERIAL}    0    ${OUTPUT DIR}
Suite Setup       Open Appium
Suite Teardown    Close Appium

*** Test Cases ***
PHONE-SMOKE-01 Purchase Survives Home And Cold Restart
    Force Stop Cat Game
    Start Cat Game
    Wait For Visual    menu    01-menu.png
    Tap UI    menu    1320    457
    Wait For Visual    intro    02-intro.png
    Tap UI    right    1280    839
    Wait For Visual    one-cat    03-one-cat.png
    ${before}=    Cat Game PID
    Should Not Be Empty    ${before}
    Press Key    3
    Wait Until Keyword Succeeds    5s    300ms    Cat Game Is Backgrounded
    Start Cat Game
    Wait For Visual    one-cat    04-warm.png
    ${warm}=    Cat Game PID
    Should Be Equal    ${before}    ${warm}
    Wait For Affordable Milk
    Buy And Press Home
    Wait Until Keyword Succeeds    5s    300ms    Cat Game Is Backgrounded
    Force Stop Cat Game
    Sleep    2s
    Start Cat Game
    Wait For Visual    menu    07-restart.png
    ${after}=    Cat Game PID
    Should Not Be Empty    ${after}
    Should Not Be Equal    ${before}    ${after}
    Tap UI    menu    1320    372
    Wait For Visual    offline    08-offline.png
    Tap UI    center    800    519
    Wait For Visual    two-cats    09-two-cats.png
    Saved Cat Count Should Be    2    10-restored-save.json
