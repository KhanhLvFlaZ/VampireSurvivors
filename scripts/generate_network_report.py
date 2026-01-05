#!/usr/bin/env python3
"""
Generate network metrics report from test results XML files
"""

import xml.etree.ElementTree as ET
import json
import sys
import os
from pathlib import Path

def parse_test_results(test_dir):
    """Parse test result XML files and extract network metrics"""
    
    metrics = {
        "latency": {"average": 0, "min": 0, "max": 0, "samples": 0},
        "bandwidth": {"upstream": 0, "downstream": 0, "peak": 0},
        "scenarios": {"twoPlayers": False, "threePlayers": False, "fourPlayers": False},
        "tests": []
    }
    
    # Find all XML files
    xml_files = list(Path(test_dir).glob("**/*.xml"))
    
    if not xml_files:
        print(f"No test results found in {test_dir}")
        return metrics
    
    for xml_file in xml_files:
        try:
            tree = ET.parse(xml_file)
            root = tree.getroot()
            
            # Extract test cases
            for testcase in root.findall('.//testcase'):
                test_name = testcase.get('name', '')
                classname = testcase.get('classname', '')
                time = float(testcase.get('time', 0))
                
                # Extract metrics from test output
                system_out = testcase.find('system-out')
                if system_out is not None and system_out.text:
                    output = system_out.text
                    
                    # Parse latency metrics
                    if 'Latency' in output or 'latency' in output:
                        metrics["latency"]["samples"] += 1
                    
                    # Track scenario success
                    if 'TwoPlayers' in test_name and 'PASS' in output:
                        metrics["scenarios"]["twoPlayers"] = True
                    elif 'ThreePlayer' in test_name and 'PASS' in output:
                        metrics["scenarios"]["threePlayers"] = True
                    elif 'FourPlayer' in test_name and 'PASS' in output:
                        metrics["scenarios"]["fourPlayers"] = True
                
                # Add test result
                failure = testcase.find('failure')
                status = 'FAIL' if failure is not None else 'PASS'
                
                metrics["tests"].append({
                    "name": test_name,
                    "class": classname,
                    "status": status,
                    "duration": time
                })
        
        except ET.ParseError as e:
            print(f"Error parsing {xml_file}: {e}")
            continue
    
    # Calculate averages
    if metrics["tests"]:
        total_time = sum(t["duration"] for t in metrics["tests"])
        metrics["latency"]["average"] = total_time / len(metrics["tests"])
    
    return metrics

def main():
    if len(sys.argv) < 2:
        print("Usage: generate_network_report.py <test_results_dir> [--output output.json]")
        sys.exit(1)
    
    test_dir = sys.argv[1]
    output_file = "network_metrics.json"
    
    # Parse command line arguments
    if "--output" in sys.argv:
        idx = sys.argv.index("--output")
        if idx + 1 < len(sys.argv):
            output_file = sys.argv[idx + 1]
    
    # Generate metrics
    metrics = parse_test_results(test_dir)
    
    # Write report
    with open(output_file, 'w') as f:
        json.dump(metrics, f, indent=2)
    
    print(f"Network metrics report generated: {output_file}")
    print(json.dumps(metrics, indent=2))

if __name__ == "__main__":
    main()
