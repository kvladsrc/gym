#include <algorithm>
#include <climits>
#include <cmath>
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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  // Counting connected compoents C. Answer is C - 1.
  map<int, vector<int>> points_by_x;
  map<int, vector<int>> points_by_y;
  vector<pair<int, int>> all;
  for (int i = 0; i < n; ++i) {
    int buf_x, buf_y;
    std::cin >> buf_x >> buf_y;
    points_by_x[buf_x].push_back(buf_y);
    points_by_y[buf_y].push_back(buf_x);
    all.push_back({buf_x, buf_y});
  }

  vector<vector<int>> visited(2000, vector<int>(2000, 0));
  int cc = 1;
  while (!all.empty()) {
    auto cur = all.back();
    all.pop_back();

    if (visited[cur.first][cur.second] != 0) {
      continue;
    }

    vector<pair<int, int>> stack;
    stack.push_back(cur);
    while (!stack.empty()) {
      auto p = stack.back();
      stack.pop_back();
      if (visited[p.first][p.second] != 0) {
        continue;
      }
      visited[p.first][p.second] = cc;
      for (auto y : points_by_x[p.first]) {
        stack.push_back({p.first, y});
      }
      for (auto x : points_by_y[p.second]) {
        stack.push_back({x, p.second});
      }
    }

    cc++;
  }

  std::cout << cc - 2 << "\n";

  return 0;
}
