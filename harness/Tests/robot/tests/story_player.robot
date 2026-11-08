*** Settings ***
Library    Process
Library    OperatingSystem

*** Test Cases ***
Actual Player Shows All Twenty Memories Rocket Ending Choices And Two Hundred CG Switches
    ${run}=    Run Process    <PROJECT>/Build/QA/CatGameQA.exe    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/player.log
    ...    cwd=<PROJECT>    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/player    env:CATGAME_QA_STORY_ONLY=1    timeout=240s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
    ${story}=    Get File    ${OUTPUT DIR}/player/story.txt
    Should Contain    ${story}    storyPassed=true
    Should Contain    ${story}    chargedOnce=true
    Should Contain    ${story}    firstSnapshotImmutable=true
    Should Contain    ${story}    continueAndMenu=true
    Should Contain    ${story}    milestones=20
    Should Contain    ${story}    stressSwitches=200
    ${manifest}=    Evaluate    json.loads(pathlib.Path('tools/story_art/achievements-v1.json').read_text(encoding='utf-8'))    modules=json,pathlib
    ${pages}=    Create List    cg-three_breeds    cg-six_breeds    cg-all_breeds    cg-planet_cats    shop-rocket    rocket-confirmation    ending-CG    ending-statistics
    FOR    ${asset}    IN    @{manifest}[assets]
        Evaluate    $pages.append('cg-' + $asset['id'])
    END
    FOR    ${size}    IN    1920x1080    1280x720    1024x768
        FOR    ${page}    IN    @{pages}
            File Should Exist    ${OUTPUT DIR}/player/${page}-${size}.png
        END
    END
    ${raw}=    Get File    ${OUTPUT DIR}/player/story-performance.json
    ${perf}=    Evaluate    json.loads($raw)    modules=json
    Should Be Equal As Integers    ${perf}[switches]    200
    Should Be Equal As Integers    ${perf}[samples]    200
    ${raw_steps}=    Get File    ${OUTPUT DIR}/player/steps.jsonl
    ${steps}=    Evaluate    [json.loads(line) for line in $raw_steps.splitlines()]    modules=json
    ${all_pass}=    Evaluate    all(step['passed'] for step in $steps)
    Should Be True    ${all_pass}
    ${switches}=    Evaluate    sum(step['type']=='stress-complete' for step in $steps)
    Should Be Equal As Integers    ${switches}    200
    ${shots}=    Evaluate    sum(step['type']=='screenshot' for step in $steps)
    Should Be Equal As Integers    ${shots}    72
