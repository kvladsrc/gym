#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

typedef vector<vector<char>> Maze;

// Hacky and happy code. Stop DFS after marking certain amount of
// vertices.
int dfs(Maze &maze, int x, int y, int need_to_mark) {
  if (need_to_mark == 0) {
    return 0;
  }

  if (x < 0 || y < 0 || y >= static_cast<int>(maze.size()) ||
      x >= static_cast<int>(maze[0].size())) {
    return 0;
  }

  if (maze[y][x] != '.') {
    return 0;
  }

  maze[y][x] = 'V';
  int r = dfs(maze, x + 1, y, need_to_mark - 1);
  int l = dfs(maze, x - 1, y, need_to_mark - (1 + r));
  int d = dfs(maze, x, y + 1, need_to_mark - (1 + r + l));
  int u = dfs(maze, x, y - 1, need_to_mark - (1 + r + l + d));

  return 1 + r + l + d + u;
}

int main(int argc, char *argv[]) {
  int n, m, k;
  std::cin >> n >> m >> k;

  // Using traversal algorithm to mark s - k cells. Removing rest of
  // the cells.

  // Filling the maze.
  int empty_cnt = 0;
  int first_x = -1, first_y = -1;
  vector<vector<char>> maze(n, vector<char>(m));
  for (int y = 0; y < n; ++y) {
    for (int x = 0; x < m; ++x) {
      std::cin >> maze[y][x];
      if (maze[y][x] == '.') {
        if (empty_cnt == 0) {
          first_x = x;
          first_y = y;
        }
        empty_cnt++;
      }
    }
  }

  // We need to remove k empty cells by marking X on them. To achive
  // that we use DFS to mark s - k cells by V (as visited) and then
  // mark all cells with . (non visited) by X.
  int need_to_mark = empty_cnt - k;
  if (need_to_mark == dfs(maze, first_x, first_y, need_to_mark)) {
    for (int y = 0; y < n; ++y) {
      for (int x = 0; x < m; ++x) {
        if (maze[y][x] == 'V') {
          std::cout << ".";
        } else if (maze[y][x] == '.') {
          std::cout << "X";
        } else {
          std::cout << "#";
        }
      }
      std::cout << "\n";
    }
    std::cout << "\n";
  }

  return 0;
}
