*** Settings ***
Documentation     Native phone ending in the isolated Perf QA package, with seeded test funds.
Library           PhoneEnding.py    http://127.0.0.1:4723    ${ADB}    5037    ${SERIAL}    0    ${OUTPUT DIR}
Suite Setup       Open Appium
Suite Teardown    Close Appium
Test Template     Check Ending Branch

*** Test Cases ***
PHONE-ENDING-01 Cancel Buy And Continue    continue
PHONE-ENDING-02 Buy And Return To Menu    menu
