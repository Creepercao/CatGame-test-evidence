*** Settings ***
Library    Process

*** Test Cases ***
Default Path Supports Low Integrity Without Overrides
    ${run}=    Run Process    python    tools/external_debugger/test_default_paths.py    stdout=${OUTPUT DIR}/default-path.log    stderr=STDOUT
    Should Be Equal As Integers    ${run.rc}    0

External Live Debugger Auth Edits Dedup Modal Roster Persistence And Stress
    ${run}=    Run Process    python    tools/external_debugger/verify_live.py    ${OUTPUT DIR}/live    stdout=${OUTPUT DIR}/verification.log    stderr=STDOUT    timeout=120s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0

External Window Controls Render And Wire Commands
    ${run}=    Run Process    python    tools/external_debugger/verify_gui.py    ${OUTPUT DIR}/gui    stdout=${OUTPUT DIR}/gui.log    stderr=STDOUT    timeout=30s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
