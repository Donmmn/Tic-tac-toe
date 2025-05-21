import pandas as pd
import json
import os

def convert_xlsx_to_json(xlsx_path, output_json_path):
    """
    Converts an Excel file containing ending data to a JSON file
    compatible with the Unity project's Ending class structure.

    Args:
        xlsx_path (str): Path to the input Excel file (e.g., "Endingtable.xlsx").
        output_json_path (str): Path to save the output JSON file (e.g., "../../lines/endings.json").
    """
    try:
        # Read the Excel file
        # Assuming the first sheet contains the data
        df = pd.read_excel(xlsx_path, sheet_name=0)
    except FileNotFoundError:
        print(f"错误：未找到Excel文件 '{xlsx_path}'。请确保文件存在于脚本同目录下。")
        return
    except Exception as e:
        print(f"读取Excel文件时出错: {e}")
        return

    endings_list = []
    required_columns = ["结局编号", "心情值下限", "心情值上限", "玩家获胜", "标题", "文本"]
    
    # Check for required columns
    missing_columns = [col for col in required_columns if col not in df.columns]
    if missing_columns:
        print(f"错误：Excel文件中缺少以下必需的列: {', '.join(missing_columns)}")
        return

    for index, row in df.iterrows():
        try:
            ending_id = int(row["结局编号"])
            min_score = int(row["心情值下限"]) # In your C# class this seems to correspond to mood
            max_score = int(row["心情值上限"]) # In your C# class this seems to correspond to mood
            
            player_win_str = str(row["玩家获胜"]).strip().upper()
            if player_win_str == "TRUE":
                player_win = True
            elif player_win_str == "FALSE": # Excel might read 'FALSE' or 'FIASE' if typo in image
                player_win = False
            else:
                problematic_value = row['玩家获胜'] # Pre-assign to a variable
                print(f"警告：在行 {index + 2}，'玩家获胜'列的值无法识别 ('{problematic_value}')。默认为 False。")
                player_win = False

            title = str(row["标题"])
            
            # Split text by "/"
            ending_text_raw = str(row["文本"])
            ending_text = [text.strip() for text in ending_text_raw.split('/')] if pd.notna(ending_text_raw) else []

            ending_data = {
                "Id": ending_id,
                "MinScore": min_score, # Matching C# Ending class (originally MoodMin)
                "MaxScore": max_score, # Matching C# Ending class (originally MoodMax)
                "PlayerWin": player_win,
                "title": title,
                "EndingText": ending_text,
                "ImagesName": []  # No image info in the Excel, so empty list
            }
            endings_list.append(ending_data)
        except ValueError as ve:
            print(f"警告：处理行 {index + 2} 时发生值错误: {ve}。跳过此行。")
            continue
        except Exception as e:
            print(f"处理行 {index + 2} 时发生意外错误: {e}。跳过此行。")
            continue
            
    output_data = {"Endings": endings_list}

    try:
        # Ensure the output directory exists
        output_dir = os.path.dirname(output_json_path)
        if output_dir and not os.path.exists(output_dir):
            os.makedirs(output_dir)
            print(f"已创建目录: {output_dir}")

        with open(output_json_path, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, ensure_ascii=False, indent=4)
        print(f"成功将数据转换为JSON并保存到: {output_json_path}")
    except Exception as e:
        print(f"保存JSON文件时出错: {e}")

if __name__ == "__main__":
    # Assuming the script is in Assets/Resources/table/
    # And the Excel file is in the same directory
    current_script_dir = os.path.dirname(os.path.abspath(__file__))
    excel_file_name = "Endingtable.xlsx"
    excel_path = os.path.join(current_script_dir, excel_file_name)

    # Output path relative to the script's location
    # Unity expects the JSON in Assets/Resources/lines/
    # So, from Assets/Resources/table/ to Assets/Resources/lines/ is ../lines/
    output_json_relative_path = os.path.join("..", "lines", "endings.json")
    output_json_full_path = os.path.abspath(os.path.join(current_script_dir, output_json_relative_path))
    
    print(f"输入Excel文件路径: {excel_path}")
    print(f"输出JSON文件路径: {output_json_full_path}")
    
    convert_xlsx_to_json(excel_path, output_json_full_path) 