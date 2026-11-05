# 본편 데이터 모델

이 폴더는 Tech_SYM이 소유하는 본편 ScriptableObject와 대화 데이터를 보관한다.

- Configuration: ArchitectureInstallation, IntegrationEntrySettings
- Audio: AudioCueCatalog
- Cinematics: CinematicCatalog
- Chase: ChaseDefinition
- Skills: ShrinkSkill, RestoreSkill, TearSkill
- Dialogue: DialogueTable.json

본편 관리 씬은 Assets/_Scenes/Tech_SYM/Runtime에 있다. 루트 Tutorial/Stage 씬은 이번 분류 범위 밖이다.

타 작업자 씬과 공유하여 원위치를 보존한 정본:
- Assets/_Scenes/Tech_SYM/Enemies/Data/{Patrol,Stationary}.asset
- Assets/_Scenes/Tech_SYM/Memo/Models/{Memo_1001,Memo_1002}.asset

위 모델을 이 폴더에 복제하여 독립 상태를 만들지 않는다. 이관하려면 해당 소비자의 소유권과 사용자 허용 범위를 확인한다.
