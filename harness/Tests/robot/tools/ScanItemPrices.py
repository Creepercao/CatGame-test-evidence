"""Snapshot shared pure C# sources; compare item-price candidates without modifying Assets."""
from pathlib import Path
import subprocess
import re
from datetime import datetime

project = Path(__file__).resolve().parents[3]
root = project / 'Tests/results/2026-10-30/RUN-BI' / ('item-price-scan-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
root.mkdir(parents=True, exist_ok=False)
sources = ['GameState', 'GameStateFactory', 'IdleMath', 'CatCatalog', 'ItemCatalog', 'IdleBalance']
rows = ['scale,clickRate,secondCat,fourBreeds,firstItem,nextBatch,goalSeconds,completion,reached']
for scale in ['1.0', '0.75', '0.5']:
    folder = root / ('scale-' + scale)
    folder.mkdir(exist_ok=True)
    for name in sources:
        content = (project / ('Assets/Scripts/Idle/' + name + '.cs')).read_text(encoding='utf-8-sig')
        if name == 'IdleBalance':
            content, count = re.subn(r'ItemBasePriceScale = [0-9.]+', 'ItemBasePriceScale = ' + scale, content)
            if count != 1:
                raise ValueError('Expected a single item price coefficient')
        (folder / (name + '.cs')).write_text(content, encoding='utf-8')
    (folder / 'scan.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><UseAppHost>false</UseAppHost><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>', encoding='utf-8')
    (folder / 'Program.cs').write_text('''using CatGame.Idle;
using System.Globalization;
foreach(var rate in new[]{0d,0.5d,2d}) {
 var r=CurveSimulator.Run(rate);
 Console.WriteLine(string.Join(",", "''' + scale + '''", rate.ToString(CultureInfo.InvariantCulture),
 r.secondCatSeconds.ToString("F3",CultureInfo.InvariantCulture),r.firstFourBreedsSeconds.ToString("F3",CultureInfo.InvariantCulture),
 r.firstItemSeconds.ToString("F3",CultureInfo.InvariantCulture),r.nextCatBatchSeconds.ToString("F3",CultureInfo.InvariantCulture),
 r.totalSeconds.ToString("F3",CultureInfo.InvariantCulture),r.completion.ToString("F3",CultureInfo.InvariantCulture),r.reachedGoal));
 File.WriteAllLines("purchases-"+rate.ToString(CultureInfo.InvariantCulture)+".txt",r.purchases);
 if(!r.reachedGoal || r.completion < .90 || (!double.IsPositiveInfinity(r.completionSeconds) && r.totalSeconds-r.completionSeconds>3)) return 1;
}
return 0;''', encoding='utf-8')
    run = subprocess.run(['dotnet', 'run', '--project', 'scan.csproj', '--verbosity', 'quiet'], cwd=folder, capture_output=True, text=True, encoding='utf-8', errors='replace')
    (folder / 'run.txt').write_text(run.stdout + run.stderr, encoding='utf-8')
    if run.returncode:
        raise RuntimeError(str(folder) + ': candidate failed; inspect run.txt')
    rows.extend(line for line in run.stdout.splitlines() if line.startswith(scale + ','))
(root / 'comparison.csv').write_text('\n'.join(rows)+'\n', encoding='utf-8')
print('\n'.join(rows))
