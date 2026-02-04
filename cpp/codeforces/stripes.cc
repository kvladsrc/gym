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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    vector<vector<char>> table(8, vector<char>(8));

    for (int i = 0; i < 8; ++i) {
      for (int j = 0; j < 8; ++j) {
        std::cin >> table[j][i];
      }
    }

    char res;
    bool done_row = true;
    bool done_col = true;

    for (int i = 0; i < 8; ++i) {
      // W for wrong;
      char first_col = table[i][0] == 'B' ? 'B' : 'W';
      char first_row = table[0][i] == 'R' ? 'R' : 'W';

      for (int j = 1; j < 8; ++j) {
        if (table[i][j] != first_col || table[i][j] == '.') {
          done_col = false;
        }

        if (table[j][i] != first_row || table[j][i] == '.') {
          done_row = false;
        }
      }

      if (done_col) {
        res = first_col;
        break;
      }

      if (done_row) {
        res = first_row;
        break;
      }

      done_col = done_row = true;
    }

    std::cout << res << "\n";
  }

  return 0;
}
