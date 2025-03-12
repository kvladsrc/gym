#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int m = 0;
    std::cin >> n >> m;

    vector<vector<char>> table(n, vector<char>(m));
    for (auto &r : table) {
      for (auto &c : r) {
        std::cin >> c;
      }
    }

    int res_x = -1;
    int res_y = -1;

    for (size_t row = 0; row < table.size(); ++row) {
      for (size_t col = 0; col < table.front().size(); ++col) {
        if (table[row][col] == '#') {
          res_x = col + 1;
          break;
        }
      }

      if (res_x != -1) {
        break;
      }
    }

    for (size_t col = 0; col < table.front().size(); ++col) {
      for (size_t row = 0; row < table.size(); ++row) {
        if (table[row][col] == '#') {
          res_y = row + 1;
          break;
        }
      }

      if (res_y != -1) {
        break;
      }
    }

    std::cout << res_y << " " << res_x << "\n";
  }

  return 0;
}
