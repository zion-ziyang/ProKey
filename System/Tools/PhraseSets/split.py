"""Splits the study phrase set into the three day sets of Study 3.

Input:  Assets/Resources/Task/days.txt (79+1 phrases of 5-6 words and 22-26 characters,
        selected from the MacKenzie & Soukoreff phrase set; the familiarization phrase 
        modified the "jumps over the lazy dog")
Output: day1.txt (26), day2.txt (26), day3.txt (27), written next to this script.

The split is random, so running it again produces different day sets. The sets used in the
studies are in Assets/Resources/Task/; copy new ones there only if you want to replace them.
"""
import os
import random

folder = os.path.dirname(os.path.abspath(__file__))
input_filename = os.path.join(folder, '..', '..', 'Assets', 'Resources', 'Task', 'days.txt')
output_filenames = [os.path.join(folder, name) for name in ('day1.txt', 'day2.txt', 'day3.txt')]

with open(input_filename, 'r', encoding='utf-8') as f:
    lines = [line.strip() for line in f if line.strip()]

random.shuffle(lines)

parts = [lines[0:26], lines[26:52], lines[52:]]
for filename, part in zip(output_filenames, parts):
    with open(filename, 'w', encoding='utf-8') as f:
        f.write('\n'.join(part))

print(f"{os.path.basename(input_filename)} ({len(lines)} phrases) was split into:")
for filename, part in zip(output_filenames, parts):
    print(f"  {os.path.basename(filename)} ({len(part)} phrases)")
