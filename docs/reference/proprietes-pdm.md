# Proprietes PDM

## Proprietes standard

Chaque document TopSolid possede des proprietes systeme accessibles via `IPdm` :

| Propriete | Getter | Setter | Notes |
|-----------|--------|--------|-------|
| Nom | `IPdm.GetName(pdmId)` | `IPdm.SetName(pdmId, value)` | Colonne "Nom" dans l'arbre PDM |
| Designation | `IPdm.GetDescription(pdmId)` | `IPdm.SetDescription(pdmId, value)` | Colonne "Designation" |
| Reference | `IPdm.GetPartNumber(pdmId)` | `IPdm.SetPartNumber(pdmId, value)` | Colonne "Reference" |
| Fabricant | `IPdm.GetManufacturer(pdmId)` | `IPdm.SetManufacturer(pdmId, value)` | |
| Ref. fabricant | `IPdm.GetManufacturerPartNumber(pdmId)` | `IPdm.SetManufacturerPartNumber(pdmId, value)` | |
| Proprietaire | `IPdm.GetOwner(pdmId)` | — | Lecture seule |

### Acces par ElementId (niveau parametre)

On peut aussi acceder aux proprietes via `IParameters` pour obtenir l'`ElementId` du parametre :

| Propriete | Methode |
|-----------|---------|
| Designation | `IParameters.GetDescriptionParameter(docId)` |
| Reference | `IParameters.GetPartNumberParameter(docId)` |
| Fabricant | `IParameters.GetManufacturerParameter(docId)` |
| Ref. fabricant | `IParameters.GetManufacturerPartNumberParameter(docId)` |
| Ref. complementaire | `IParameters.GetComplementaryPartNumberParameter(docId)` |

## Proprietes utilisateur

Les proprietes utilisateur sont des champs custom definis par l'entreprise. Tres utilisees pour le filtrage dans les nomenclatures.

**Exemples courants** :
- "Type de production" : Achete / Fabrique
- "Mise en plan necessaire" : Oui / Non
- "Categorie" : Tole / Profile / Visserie

### Lecture/ecriture rapide (niveau PDM)

```csharp
// Lecture
string value = TopSolidHost.Pdm.GetTextUserProperty(pdmId, "Type de production");

// Ecriture (dans un modify_script)
TopSolidHost.Pdm.SetTextUserProperty(pdmId, "Type de production", "Fabrique");
```

### Lecture/ecriture avancee (niveau parametre)

```csharp
// Chercher le parametre de la propriete utilisateur dans le document
ElementId paramId = TopSolidHost.Parameters.SearchUserPropertyParameter(docId, propDefDocId);
if (!paramId.IsEmpty)
{
    string value = TopSolidHost.Parameters.GetTextValue(paramId);
}
```

### Proprietes d'occurrence

Dans les assemblages, on peut ajouter ou surcharger des proprietes sur une **occurrence** specifique (pas sur le document entier). Cela se fait via l'onglet "Construction" dans TopSolid.
