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

int main(int argc, char *argv[]) {
  int r, c;
  std::cin >> r >> c;
  unordered_map<int, bool> rows, columns;
  for (int row = 1; row <= r; ++row) {
    for (int col = 1; col <= c; ++col) {
      char buf;
      std::cin >> buf;
      if (buf == 'S') {
        rows[row] = true;
        columns[col] = true;
      }
    }
  }

  int eated_rows = 0;
  int res = 0;
  for (int row = 1; row <= r; ++row) {
    if (!rows[row]) {
      eated_rows++;
      res += c;
    }
  }

  for (int col = 1; col <= c; ++col) {
    if (!columns[col]) {
      res += r - eated_rows;
    }
  }

  std::cout << res << "\n";

  return 0;
}
