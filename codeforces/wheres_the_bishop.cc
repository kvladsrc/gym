#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    vector<vector<char>> board(8, vector<char>(8));
    int res_x = -1, res_y = -1;

    for (int y = 0; y < 8; ++y) {
      for (int x = 0; x < 8; ++x) {
        std::cin >> board[x][y];

        if (res_x != -1) continue;

        if (x > 0 && x < 7 && y > 0 && y < 7) {
          if (board[x - 1][y - 1] == '#' && board[x + 1][y - 1] == '#') {
            res_x = x + 1;
            res_y = y + 1;
          }
        }
      }
    }

    std::cout << res_y << " " << res_x << "\n";
  }

  return 0;
}
