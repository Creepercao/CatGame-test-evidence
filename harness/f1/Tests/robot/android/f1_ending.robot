*** Settings ***
Documentation    Ordinary code6 APK ending via Appium on owned AVD, seeded funds in explicit QA fixture.
Library          Process

*** Test Cases ***
F1-END-01 Rocket Cancel CG Statistics Continue And Menu
    ${run}=    Run Process    ${PYTHON}    ${CURDIR}/f1_ending_runner.py    --adb    ${ADB}
    ...    --fixture    ${FIXTURE}    --output    ${OUTPUT DIR}/runtime
    ...    stdout=${OUTPUT DIR}/runner.log    stderr=STDOUT    timeout=180s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
