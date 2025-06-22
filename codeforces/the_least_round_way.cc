#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

void min_path(vector<vector<int64_t>>& table, vector<vector<char>>& path) {
  int size = table.size();

  for (int i = 1; i < size; ++i) {
    table[size - 1 - i][size - 1] += table[size - i][size - 1];
    path[size - 1 - i][size - 1] = 'D';

    table[size - 1][size - 1 - i] += table[size - 1][size - i];
    path[size - 1][size - 1 - i] = 'R';
  }

  for (int i = 1; i < size; ++i) {
    for (int j = 1; j < size; ++j) {
      if (table[size - 1 - i][size - j] <= table[size - i][size - 1 - j]) {
        table[size - 1 - i][size - 1 - j] += table[size - 1 - i][size - j];
        path[size - 1 - i][size - 1 - j] = 'R';
      } else {
        table[size - 1 - i][size - 1 - j] += table[size - i][size - 1 - j];
        path[size - 1 - i][size - 1 - j] = 'D';
      }
    }
  }
}

void print_path(vector<vector<char>>& path) {
  int i = 0;
  int j = 0;
  int size = path.size() - 1;

  while (i != size || j != size) {
    std::cout << path[i][j];
    if (path[i][j] == 'D') {
      i++;
    } else {
      j++;
    }
  }
  std::cout << "\n";
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  vector<vector<int64_t>> five(n, vector<int64_t>(n, 0));
  vector<vector<int64_t>> two(n, vector<int64_t>(n, 0));
  bool has_zero = false;
  int zero_i = 0;
  int zero_j = 0;

  for (int i = 0; i < n; ++i) {
    for (int j = 0; j < n; ++j) {
      int64_t buf = 0;
      std::cin >> buf;

      if (buf == 0) {
        zero_i = i;
        zero_j = j;
        has_zero = true;
      }

      auto buf_cpy = buf;
      while ((buf_cpy % 5) == 0 && buf_cpy != 0) {
        five[i][j]++;
        buf_cpy /= 5;
      }

      buf_cpy = buf;
      while (buf_cpy % 2 == 0 && buf_cpy != 0) {
        two[i][j]++;
        buf_cpy /= 2;
      }
    }
  }

  vector<vector<char>> five_path(n, vector<char>(n));
  vector<vector<char>> two_path(n, vector<char>(n));
  min_path(two, two_path);
  min_path(five, five_path);

  if (has_zero && two.front().front() != 0 && five.front().front() != 0) {
    std::cout << 1 << "\n";
    int i = 0;
    int j = 0;
    while (i < zero_i) {
      std::cout << 'D';
      i++;
    }
    while (j < zero_j) {
      std::cout << 'R';
      j++;
    }
    while (i < n - 1) {
      std::cout << 'D';
      i++;
    }
    while (j < n - 1) {
      std::cout << 'R';
      j++;
    }
    return 0;
  }

  if (two.front().front() < five.front().front()) {
    std::cout << two.front().front() << "\n";
    print_path(two_path);
  } else {
    std::cout << five.front().front() << "\n";
    print_path(five_path);
  }

  return 0;
}
