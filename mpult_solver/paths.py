"""Repository-relative locations shared by the implementation."""
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASELINE=ROOT/"assets/model/baseline.log"
ANCHOR_FILE=ROOT/"assets/model/anchor.dat"
