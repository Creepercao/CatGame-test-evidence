*** Settings ***
Documentation     Native phone layout, lock recovery and offline cold start. Uses the smoke-created save.
Library           PhoneGame.py    http://127.0.0.1:4723    ${ADB}    5037    ${SERIAL}    0    ${OUTPUT DIR}
Suite Setup       Open Appium
Suite Teardown    Close Appium

*** Variables ***
${CAT_COUNT}    2
${HOME_VISUAL}    two-cats

*** Test Cases ***
PHONE-LAYOUT-01 Native Cutout Shop And Achievements
    Wait For Visual    ${HOME_VISUAL}    layout-01-home.png
    Swipe Shop    layout-02-before.png    layout-03-after.png
    Press Key    4
    Wait For Visual    pause    layout-04-settings.png
    Tap UI    center    505    402
    Sleep    500ms
    Capture    layout-05-achievements.png
    Tap UI    center    505    345
    Sleep    500ms
    Capture    layout-06-catalog.png
    Press Key    4
    Wait For Visual    ${HOME_VISUAL}    layout-07-resumed.png

PHONE-PET-01 Touch Increments Persisted Counter
    Pet Visible Cat
    Wait For Visual    ${HOME_VISUAL}    pet-restored.png

PHONE-LOCK-01 Original Process Survives Lock And Unlock
    ${before}=    Cat Game PID
    Lock Screen
    Wait Until Keyword Succeeds    8s    500ms    Screen Should Be Locked    true
    Unlock Screen
    Wait Until Keyword Succeeds    15s    500ms    Screen Should Be Locked    false
    Capture    lock-01-unlocked.png
    Saved Cat Count Should Be    ${CAT_COUNT}    lock-01-save.json
    ${after}=    Cat Game PID
    Should Be Equal    ${before}    ${after}
    Tap UI    left    330    43
    Wait For Visual    pause    lock-02-touch.png
    Press Key    4
    Capture    lock-03-restored.png

PHONE-OFFLINE-01 Cold Start With No Default Network
    [Teardown]    Restore Network
    Begin Offline Test
    Force Stop Cat Game
    Sleep    2s
    Start Cat Game
    Wait For Visual    menu    offline-01-menu.png
    Tap UI    menu    1320    372
    Wait For Visual    offline    offline-02-summary.png
    Tap UI    center    800    519
    Wait For Visual    two-cats    offline-03-resumed.png
    Phone Should Be Offline
    Buy Offline Cat
    Force Stop Cat Game
    Sleep    2s
    Start Cat Game
    Wait For Visual    menu    offline-05-restart.png
    Tap UI    menu    1320    372
    Wait For Visual    offline    offline-06-summary.png
    Tap UI    center    800    519
    Sleep    1s
    Capture    offline-07-three-cats.png
    Saved Cat Count Should Be    3    offline-08-restored-save.json
