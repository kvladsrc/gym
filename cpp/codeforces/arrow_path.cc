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

void reachable_from_start(vector<vector<char>> &field, int row, int column,
                          vector<vector<bool>> &visited) {
  if (visited[row][column]) {
    return;
  }

  visited[row][column] = true;
  int size = field[0].size();

  if (column > 0) {
    if (field[row][column - 1] == '<') {
      reachable_from_start(field, row, column - 2, visited);
    }
  }

  if (column < size - 1) {
    if (field[row][column + 1] == '>') {
      reachable_from_start(field, row, column + 2, visited);
    }
  }

  if (field[1 - row][column] == '>') {
    reachable_from_start(field, 1 - row, column + 1, visited);
  } else {
    reachable_from_start(field, 1 - row, column - 1, visited);
  }
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<vector<char>> field(2, vector<char>(n));
    for (int i = 0; i < n; ++i) {
      std::cin >> field[0][i];
    }

    for (int i = 0; i < n; ++i) {
      std::cin >> field[1][i];
    }

    vector<vector<bool>> visited(2, vector<bool>(n, false));

    reachable_from_start(field, 0, 0, visited);
    if (visited[1][n - 1]) {
      std::cout << "YES" << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
