# Connecteur TopSolid Automation

Ce document décrit le fonctionnement du connecteur natif utilisant l'API Automation de TopSolid.

## Architecture

Le serveur MCP communique avec TopSolid via des **Named Pipes** .NET. Contrairement à l'ancienne architecture utilisant un Bridge WCF (HTTP), cette connexion est directe et ne nécessite aucun processus intermédiaire.

## Configuration logicielle requise

- **TopSolid 7.15+** (développé et testé sur 7.20)
- **TopSolid.Kernel.Automating.dll** : Cette DLL doit être présente dans le répertoire d'installation de TopSolid.

## Initialisation

La connexion est initialisée au démarrage du serveur via la classe `TopSolidConnector`.

```csharp
var connector = new TopSolidConnector();
connector.Connect();
```

Le serveur tente de se connecter à une instance de TopSolid déjà ouverte. S'il n'en trouve pas au démarrage, il reste en attente.

### Reconnexion automatique (Lazy)
Si TopSolid est lancé APRÈS le serveur MCP :
- Les outils d'interrogation en direct (`topsolid_get_state`, `topsolid_execute_script`) tenteront automatiquement de se reconnecter une fois au moment de l'appel.
- Si la reconnexion réussit, l'outil s'exécute normalement.
- Si elle échoue (TopSolid toujours fermé), l'outil retourne un message indiquant que TopSolid n'est pas connecté.

## Outils disponibles

### `topsolid_get_state`
Retourne le nom du document édité, son type (extension) et le projet PDM associé.

### `topsolid_execute_script`
Compile et exécute un script C# dynamique contre l'API Automation de TopSolid.

#### Fonctionnement
- **Compilateur** : `Microsoft.CSharp.CSharpCodeProvider` (.NET 4.8).
- **Wrapper** : Le code envoyé est encapsulé dans une classe `DynamicScript` avec une méthode `Run()`.
- **Namespaces inclus** : `System`, `System.Collections.Generic`, `System.Linq`, `System.Text`, `System.IO` et `TopSolid.Kernel.Automating`.
- **Transactions** : Détection automatique des mots-clés (`Set`, `Create`, `SetName`, etc.) pour ouvrir une transaction TopSolid via `StartModification`.

#### Exemple de script
```csharp
var doc = TopSolidHost.Documents.EditedDocument;
var elements = TopSolidHost.Elements.GetElements(doc);
return $"Le document contient {elements.Count} éléments.";
```

## Gestion des Erreurs

Toutes les méthodes de `TopSolidConnector` et `ScriptExecutor` sont sécurisées par des blocs `try-catch`.
- **Erreurs de compilation** : Retournées avec le numéro de ligne et le code d'erreur CS.
- **Erreurs d'exécution** : Capturées sous forme de `TargetInvocationException` pour rapporter l'erreur interne sans crasher le serveur.
- **Perte de connexion** : Si TopSolid est fermé, l'outil retourne un message standard de déconnexion.
