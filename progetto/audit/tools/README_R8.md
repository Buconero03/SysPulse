# Controlli R8

Da Python 3, senza SDK o processi Windows:

```powershell
python .\audit\tools\check_r8.py --input-zip C:\percorso\ProcessLens-v0.19.16-R7.zip --candidate . --output .\verification-r8\static-results.json
```

Richiede lo ZIP originale ricevuto. Non compila C#/XAML e non misura RAM. I test .NET e i contatori Windows sono descritti in OTTIMIZZAZIONE_RAM_R8.md. I controlli R7 presenti accanto sono storici.
