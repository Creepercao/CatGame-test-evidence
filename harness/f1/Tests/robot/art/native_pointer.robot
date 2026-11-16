*** Settings ***
Documentation    One actual desktop click into the owned QA window with per-frame input/physics/UI diagnostics.
Library          Process
Library          OperatingSystem

*** Variables ***
${MANUAL_FOCUS}    0

*** Test Cases ***
F1-POINTER-01 Native Click Reaches The Displayed Cat
    Create Directory    ${OUTPUT DIR}/runtime
    ${run}=    Run Process    ${EXE}    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/runtime
    ...    env:CATGAME_QA_SYSTEM_POINTER=1    env:CATGAME_QA_POINTER_ONLY=1
    ...    env:CATGAME_QA_MANUAL_FOCUS=${MANUAL_FOCUS}
    ...    env:CATGAME_DEBUG_DISABLE=1
    ...    stdout=${OUTPUT DIR}/stdout.txt    stderr=${OUTPUT DIR}/stderr.txt
    ...    timeout=120s    on_timeout=terminate
    File Should Exist    ${OUTPUT DIR}/runtime/pointer-probe.jsonl
    ${raw}=    Get File    ${OUTPUT DIR}/runtime/pointer-probe.jsonl
    Log    ${raw}
    Should Be Equal As Integers    ${run.rc}    0
    ${rows}=    Evaluate    [json.loads(line) for line in $raw.splitlines()]    modules=json
    Should Be True    any(row['down'] for row in $rows)
    Should Be Equal As Integers    ${rows}[-1][clicks]    1
