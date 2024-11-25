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
    int n, m;
    std::cin >> n >> m;

    vector<vector<char>> carpet(n, vector<char>(m));
    for (int i = 0; i < n; ++i) {
      for (int j = 0; j < m; ++j) {
        std::cin >> carpet[i][j];
      }
    }

    string tmpl("vika");
    size_t search_for = 0;
    int col = 1;
    while (col <= m && search_for < tmpl.size()) {
      for (auto row : carpet) {
        if (row[col - 1] == tmpl[search_for]) {
          search_for++;
          break;
        }
      }
      col++;
    }

    if (search_for == tmpl.size()) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
