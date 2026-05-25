#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

struct Result {
  bool calculated;
  int dist;
};

static int edit_distance(const string& str1, int str1_pos, const string& str2,
                         int str2_pos, vector<vector<Result>>& table) {
  if (str1_pos == static_cast<int>(str1.size())) {
    return str2.size() - str2_pos;
  }

  if (str2_pos == static_cast<int>(str2.size())) {
    return str1.size() - str1_pos;
  }

  if (table[str1_pos][str2_pos].calculated) {
    return table[str1_pos][str2_pos].dist;
  }

  if (str1[str1_pos] == str2[str2_pos]) {
    return edit_distance(str1, str1_pos + 1, str2, str2_pos + 1, table);
  }

  int const case1 =
      edit_distance(str1, str1_pos + 1, str2, str2_pos + 1, table) + 1;
  int const case2 =
      edit_distance(str1, str1_pos + 1, str2, str2_pos, table) + 1;
  int const case3 =
      edit_distance(str1, str1_pos, str2, str2_pos + 1, table) + 1;

  int const tmp_min1 = case1 < case2 ? case1 : case2;
  table[str1_pos][str2_pos].dist = tmp_min1 < case3 ? tmp_min1 : case3;
  table[str1_pos][str2_pos].calculated = true;

  return table[str1_pos][str2_pos].dist;
}

int main() {
  string str1;
  string str2;
  std::cin >> str1 >> str2;
  vector<vector<Result>> table;
  table.resize(str1.size(), vector<Result>(str2.size()));
  std::cout << edit_distance(str1, 0, str2, 0, table) << '\n';
  return 0;
}
