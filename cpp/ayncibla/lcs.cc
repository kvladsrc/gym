#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

enum arrows { up, left, up_left };

static vector<int> lcs2(const vector<int>& a, const vector<int>& b) {
  vector<vector<int>> table;
  vector<vector<arrows>> table_arrows;
  table.resize(a.size() + 1, vector<int>(b.size() + 1));
  table_arrows.resize(a.size() + 1, vector<arrows>(b.size() + 1));

  for (size_t i = 0; i <= a.size(); ++i) {
    table[i][0] = 0;
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

  vector<int> res(table[a.size()][b.size()]);
  int x = a.size();
  int y = b.size();
  size_t cur = 0;
  while (cur < res.size()) {
    switch (table_arrows[x][y]) {
      case up_left:
        res[cur++] = a[x - 1];
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
  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (size_t i = 0; i < n; i++) {
    std::cin >> a[i];
  }

  size_t m = 0;
  std::cin >> m;
  vector<int> b(m);
  for (size_t i = 0; i < m; i++) {
    std::cin >> b[i];
  }

  std::cout << lcs2(a, b).size() << '\n';
}
