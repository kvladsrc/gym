#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <regex>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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

const int DEFAULT_DSU_SIZE = 100;

class dsu {
public:
  dsu() : dsu(DEFAULT_DSU_SIZE) {}
  dsu(int size) {
    parent.resize(size);
    rank.resize(size, 0);
    for (int idx = 0; idx < size; ++idx) {
      parent[idx] = idx;
    }
  }
  bool connected(int a, int b) {
    auto pa = find_parent(a);
    auto pb = find_parent(b);
    return pa == pb;
  }
  void join(int a, int b) {
    if (connected(a, b)) {
      return;
    }

    auto pa = find_parent(a);
    auto pb = find_parent(b);
    if (rank[pa] > rank[pb]) {
      parent[pb] = pa;
    } else if (rank[pb] > rank[pa]) {
      parent[pa] = pb;
    } else {
      parent[pa] = pb;
      rank[pb]++;
    }
  }

private:
  vector<int> rank;
  vector<int> parent;

  int find_parent(int el) {
    if (parent[el] == el) {
      return el;
    }
    return find_parent(parent[el]);
  }
};

using Maze = vector<vector<char>>;

struct cell {
  int x, y;

  cell left() {
    cell res;
    res.x = x - 1;
    res.y = y;
    return res;
  }

  cell right() {
    cell res;
    res.x = x + 1;
    res.y = y;
    return res;
  }

  cell up() {
    cell res;
    res.x = x;
    res.y = y - 1;
    return res;
  }

  cell down() {
    cell res;
    res.x = x;
    res.y = y + 1;
    return res;
  }

  cell() {};
  cell(int ax, int ay) : x(ax), y(ay) {};
  ~cell() {};
  int id(const Maze &maze);
  bool is_valid(const Maze &);
  bool is_empty(const Maze &);
  bool is_available(const Maze &);
};

int cell::id(const Maze &maze) {
  auto width = maze.front().size();
  auto res = width * y + x;
  return res;
}

bool cell::is_valid(const Maze &maze) {
  if (x < 0 || y < 0 || x >= static_cast<int>(maze[0].size()) ||
      y >= static_cast<int>(maze.size())) {
    return false;
  }
  return true;
}

bool cell::is_empty(const Maze &maze) {
  if (is_valid(maze)) {
    return maze[y][x] == '.';
  }
  return false;
}

bool cell::is_available(const Maze &maze) {
  if (is_valid(maze)) {
    return maze[y][x] != '#';
  }
  return false;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, m;
    std::cin >> n >> m;

    Maze maze(n, vector<char>(m));
    vector<cell> good;
    vector<cell> bad;
    for (int y = 0; y < n; ++y) {
      for (int x = 0; x < m; ++x) {
        char buf;
        std::cin >> buf;
        if (buf == 'G') {
          good.push_back(cell(x, y));
        }
        if (buf == 'B') {
          bad.push_back(cell(x, y));
        }
        maze[y][x] = buf;
      }
    }

    for (auto cur : bad) {
      if (cur.up().is_empty(maze)) {
        maze[cur.up().y][cur.up().x] = '#';
      }
      if (cur.down().is_empty(maze)) {
        maze[cur.down().y][cur.down().x] = '#';
      }
      if (cur.left().is_empty(maze)) {
        maze[cur.left().y][cur.left().x] = '#';
      }
      if (cur.right().is_empty(maze)) {
        maze[cur.right().y][cur.right().x] = '#';
      }
    }

    dsu mdsu(3000);
    for (int y = 0; y < n; ++y) {
      for (int x = 0; x < m; ++x) {
        cell cur(x, y);
        if (cur.is_available(maze)) {
          if (cur.up().is_available(maze)) {
            mdsu.join(cur.id(maze), cur.up().id(maze));
          }
          if (cur.down().is_available(maze)) {
            mdsu.join(cur.id(maze), cur.down().id(maze));
          }
          if (cur.left().is_available(maze)) {
            mdsu.join(cur.id(maze), cur.left().id(maze));
          }
          if (cur.right().is_available(maze)) {
            mdsu.join(cur.id(maze), cur.right().id(maze));
          }
        }
      }
    }

    cell end(m - 1, n - 1);
    bool res = true;
    for (auto g : good) {
      if (!mdsu.connected(end.id(maze), g.id(maze))) {
        res = false;
        break;
      }
    }

    for (auto b : bad) {
      if (mdsu.connected(end.id(maze), b.id(maze))) {
        res = false;
        break;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
