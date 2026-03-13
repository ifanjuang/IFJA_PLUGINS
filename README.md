# Mater2026 — Revit PBR Material Manager

Plugin Revit 2026 pour la gestion avancée des matériaux PBR (Physically Based Rendering).
Développé par **IFJ Architecture**.

## Fonctionnalités

### Gestion des matériaux du projet
- **Liste des matériaux** avec filtre texte et tri alphabétique
- **Indicateur d'utilisation** : les matériaux utilisés dans le modèle sont en gras
- **Filtre "Unused"** : affiche ou masque les matériaux non utilisés
- **Pick** : sélectionner un matériau en cliquant sur une face du modèle 3D
- **Assign** : peindre un matériau sur plusieurs faces sélectionnées (`Document.Paint`)

### Workflow PBR (textures)
- **6 canaux de maps** : Albedo, Bump/Normal/Depth, Roughness, Reflection, Refraction, Illumination
- **Détection automatique** des maps par nom de fichier (patterns : `diff`, `albedo`, `normal`, `rough`, `metal`, etc.)
- **Assignation manuelle** : browse ou drag & drop par slot
- **Paramètres unifiés** : dimensions réelles (cm), rotation, teinte (tint color)
- **Propriétés UnifiedBitmap** : chemin, échelle, angle, couleur de teinte

### Navigation de textures
- **Arborescence (TreeView)** avec chargement lazy des sous-dossiers
- **Grille de miniatures** avec tailles configurables (128, 256, 512, 1024 px)
- **Génération de miniatures** en batch avec barre de progression
- **Fil d'Ariane (breadcrumb)** cliquable pour remonter dans l'arborescence

### Tile Patterns
- Création de **motifs de tuiles** via `FillGrid` avec espacement précis
- Support du **décalage demi-tuile** (offset)
- Suppression automatique du motif quand Tiles X/Y = 0

### Persistance
- **Extensible Storage** Revit : le dossier racine des textures est sauvegardé dans le document
- Le paramètre survit entre les sessions Revit (lié au fichier `.rvt`)

## Architecture

```
MaterRevitAddin/
├── App.cs                  # IExternalApplication — ribbon, ExternalEvent handlers
├── Mater.cs                # Core : modèles, services, ViewModel, handlers
├── MaterWindow.xaml         # Interface WPF (3 panneaux)
├── MaterWindow.xaml.cs      # Code-behind (événements UI)
├── Mater2026.addin          # Manifeste plugin Revit
├── Mater2026.csproj         # Configuration projet .NET 8
└── Resources/
    ├── icon16.png           # Icône ribbon 16px
    ├── icon32.png           # Icône ribbon 32px
    └── folder16.png         # Icône folder
```

### Composants principaux

| Classe | Rôle |
|--------|------|
| `App` | Point d'entrée, ribbon UI, création des ExternalEvent handlers |
| `MaterViewModel` | MVVM ViewModel principal |
| `MaterUiState` | État du formulaire (nom, dimensions, rotation, tint, tiles) |
| `RevitMaterialService` | Création/modification matériaux et AppearanceAssetElement |
| `TilePatternService` | Gestion des FillPattern via FillGrid |
| `MapDetectionService` | Auto-détection des types de map par nom de fichier |
| `RevitStorageService` | Persistance Extensible Storage |
| `PickMaterialHandler` | IExternalEventHandler — sélection de matériau par face |
| `ApplyMaterialHandler` | IExternalEventHandler — application des modifications |
| `AssignMaterialHandler` | IExternalEventHandler — peinture de faces multiples |

### Pattern ExternalEvent

Les opérations Revit API ne peuvent être exécutées que depuis le thread Revit.
Le plugin utilise le pattern `IExternalEventHandler` + `ExternalEvent.Raise()` pour exécuter
le code API de manière thread-safe depuis l'interface WPF :

```
WPF Thread                    Revit Thread
    │                              │
    ├─ Handler.Request(...)        │
    ├─ ExternalEvent.Raise()  ───► │
    │                              ├─ Handler.Execute(UIApplication)
    │                              ├─ Transaction + API calls
    │  ◄─── callback ────────────  ├─ callback?.Invoke(result)
    │                              │
```

## Prérequis

- **Revit 2026** installé
- **.NET 8.0 SDK** (Windows)
- Variable d'environnement `REVIT_2026` (optionnel, fallback : `C:\Program Files\Autodesk\Revit 2026`)

## Build & Déploiement

```bash
dotnet build MaterRevitAddin/Mater2026.csproj -c Release
```

Le post-build copie automatiquement dans :
```
C:\ProgramData\Autodesk\Revit\Addins\2026\Mater2026\
```

Fichiers déployés :
- `Mater2026.dll`
- `Mater2026.addin`
- Icônes (resources embarquées)

## Configuration

### Dossier racine des textures
1. Cliquer sur le bouton dossier dans le panneau gauche
2. Sélectionner le dossier racine contenant les sous-dossiers de matériaux PBR
3. Le chemin est sauvegardé dans le document Revit via Extensible Storage

### Structure attendue des textures
```
Textures/
├── Bois_Chene/
│   ├── Bois_Chene_diff.jpg      → Albedo (auto-détecté)
│   ├── Bois_Chene_normal.jpg    → Bump (auto-détecté)
│   ├── Bois_Chene_rough.jpg     → Roughness (auto-détecté)
│   └── Bois_Chene_256.jpg       → Miniature (auto-générée)
├── Marbre_Blanc/
│   ├── ...
```

### Mots-clés de détection automatique

| Canal | Mots-clés reconnus |
|-------|-------------------|
| Albedo | `diff`, `dif`, `albedo`, `basecolor`, `col`, `color`, `rgb` |
| Bump | `bump`, `normal`, `disp`, `height`, `depth` |
| Roughness | `rough`, `gloss` |
| Reflection | `refl`, `metal` |
| Refraction | `refr`, `opac`, `transp` |
| Illumination | `emit`, `glow`, `light` |

## Utilisation

1. Ouvrir un projet Revit 2026
2. Onglet **Add-Ins** → **Mater 2026**
3. **Panneau gauche** : naviguer dans l'arborescence de textures
4. **Panneau central** : parcourir les miniatures, générer les thumbnails
5. **Panneau droit** :
   - Sélectionner/filtrer un matériau existant
   - Configurer les maps PBR, dimensions, rotation, teinte
   - **Create** : crée un nouveau matériau avec l'apparence configurée
   - **Update** : met à jour l'apparence du matériau sélectionné
   - **Replace** : remplace l'apparence d'un matériau existant
   - **Assign** : peint le matériau sélectionné sur des faces du modèle

## Licence

Propriétaire — IFJ Architecture. Tous droits réservés.
