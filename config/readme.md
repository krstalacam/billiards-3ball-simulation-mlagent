
cd /d "D:\krstlcm Workspace\Unity\UnityProjects\1.Seviye\threeballbilliards\three-ball-billiards-agent"

conda activate mlagents


mlagents-learn config\MyBehavior.yaml --run-id=billiard_agent_v16 --resume --env="Build\three-ball-billiards-agent.exe"




mlagents-learn config\MyBehavior.yaml --run-id=billiard_agent_v9new --resume


--force



anaconda hangı konumda acılırsa result orada olusturuyor o yuzden ıstersen once cd d: falan yapip 