#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  vector<string> table;
  for (int i = 0; i < 1000; ++i) {
    int buf = 8 * i;
    if (buf < 1000) {
      string s;
      s = std::to_string(buf);
      table.push_back(s);
    }
  }

  string s;
  std::cin >> s;
  vector<int> pos(table.size(), 0);
  int res = -1;
  for (auto c : s) {
    if (res != -1) break;

    for (size_t i = 0; i < table.size(); ++i) {
      if (table[i][pos[i]] == c) {
        pos[i]++;
      }
      if (pos[i] >= static_cast<int>(table[i].size())) {
        res = i;
        break;
      }
    }
  }

  if (res != -1) {
    std::cout << "YES"
              << "\n";
    int num = std::stoi(table[res]);
    std::cout << num << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
