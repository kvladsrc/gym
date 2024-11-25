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
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int c_to_i(char c) { return c - 'a'; }

int solve_rec(int c, int start, int end, vector<vector<int>> &char_table) {
  if (start == end) {
    if (char_table[start][c] == char_table[start - 1][c]) {
      return 1;
    } else {
      return 0;
    }
  }

  int mid = (start + end) / 2;
  int c_in_left = char_table[mid][c] - char_table[start - 1][c];
  int c_in_right = char_table[end][c] - char_table[mid][c];
  int case1 = (mid - start + 1) - c_in_left;
  case1 += solve_rec(c + 1, mid + 1, end, char_table);
  int case2 = (end - mid) - c_in_right;
  case2 += solve_rec(c + 1, start, mid, char_table);

  return min(case1, case2);
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<vector<int>> char_table(n + 1, vector<int>(26));
    for (auto &i : char_table.front()) {
      i = 0;
    }

    for (int i = 1; i <= n; ++i) {
      char buf;
      std::cin >> buf;
      char_table[i] = char_table[i - 1];
      char_table[i][c_to_i(buf)]++;
    }

    std::cout << solve_rec(0, 1, n, char_table) << "\n";
  }

  return 0;
}
