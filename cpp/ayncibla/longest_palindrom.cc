#include <boost/range/algorithm/reverse.hpp>
#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

enum arrows { up, left, up_left };

static string lcs(const string& a, const string& b) {
  vector<vector<int>> table;
  vector<vector<arrows>> table_arrows;
  table.resize(a.size() + 1, vector<int>(b.size() + 1));
  table_arrows.resize(a.size() + 1, vector<arrows>(b.size() + 1));

  for (size_t i = 0; i <= a.size(); ++i) {
    table[i][0] = 0;
  }

  for (size_t i = 0; i <= b.size(); ++i) {
    table[0][i] = 0;
  }

  for (size_t i = 1; i <= a.size(); ++i) {
    for (size_t j = 1; j <= b.size(); ++j) {
      if (a[i - 1] == b[j - 1]) {
        table[i][j] = table[i - 1][j - 1] + 1;
        table_arrows[i][j] = up_left;
      } else {
        if (table[i][j - 1] > table[i - 1][j]) {
          table[i][j] = table[i][j - 1];
          table_arrows[i][j] = left;
        } else {
          table[i][j] = table[i - 1][j];
          table_arrows[i][j] = up;
        }
      }
    }
  }

  string res;
  int x = a.size();
  int y = b.size();
  while (x >= 1 && y >= 1) {
    switch (table_arrows[x][y]) {
      case up_left:
        res += a[x - 1];
        x--;
        y--;
        break;
      case left:
        y--;
        break;
      default:
        // up
        x--;
        break;
    }
  }

  return res;
}

int main() {
  string s;
  std::cin >> s;
  string rs(s);
  boost::range::reverse(rs);
  std::cout << lcs(s, rs) << "\n";
}
