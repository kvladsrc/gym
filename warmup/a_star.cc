#include <cstdlib>
#include <iostream>
#include <queue>
#include <string>
#include <vector>

using std::abs;
using std::priority_queue;
using std::string;
using std::vector;

using Maze = vector<vector<char>>;

struct cell {
  int x, y;

  cell left() const {
    cell res{};
    res.x = x - 1;
    res.y = y;
    return res;
  }

  cell right() const {
    cell res{};
    res.x = x + 1;
    res.y = y;
    return res;
  }

  cell up() const {
    cell res{};
    res.x = x;
    res.y = y - 1;
    return res;
  }

  cell down() const {
    cell res{};
    res.x = x;
    res.y = y + 1;
    return res;
  }

  bool is_empty(Maze & /*maze*/) const;
  bool reachable_from(Maze & /*maze*/, cell /*other*/) const;
};

bool cell::is_empty(Maze &maze) const {
  if (x < 0 || y < 0 || x >= static_cast<int>(maze[0].size()) ||
      y >= static_cast<int>(maze.size())) {
    return false;
  }
  return maze[y][x] == '.';
}

static int dist(cell a, cell b) { return abs(a.x - b.x) + abs(a.y - b.y); }

class CompareDistance {
 private:
  cell target;

 public:
  explicit CompareDistance(const cell ref) : target(ref) {}

  bool operator()(const cell &p1, const cell &p2) const {
    return dist(p1, target) > dist(p2, target);
  }
};

bool cell::reachable_from(Maze &maze, cell other) const {
  if (!is_empty(maze)) {
    return false;
  }

  cell cur{};
  cur.x = x;
  cur.y = y;
  CompareDistance const cf(cur);

  priority_queue<cell, vector<cell>, CompareDistance> a_star_heap(cf);
  a_star_heap.push(other);

  while (!a_star_heap.empty()) {
    auto v = a_star_heap.top();
    a_star_heap.pop();

    if (!v.is_empty(maze)) {
      continue;
    }

    if (x == v.x && y == v.y) {
      return true;
    }

    maze[v.y][v.x] = 'V';
    a_star_heap.push(v.left());
    a_star_heap.push(v.right());
    a_star_heap.push(v.up());
    a_star_heap.push(v.down());
  }

  return false;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  // Filling the maze.
  vector<vector<char>> maze(n, vector<char>(m));
  for (int y = 0; y < n; ++y) {
    for (int x = 0; x < m; ++x) {
      std::cin >> maze[y][x];
    }
  }

  int q = 0;
  std::cin >> q;
  while ((q--) != 0) {
    cell start{};
    cell end{};
    std::cin >> start.x >> start.y >> end.x >> end.y;
    Maze tmp(maze);
    if (end.reachable_from(tmp, start)) {
      for (int y = 0; y < n; ++y) {
        for (int x = 0; x < m; ++x) {
          std::cout << tmp[y][x];
        }
        std::cout << "\n";
      }
      std::cout << "\n";
    }
  }

  return 0;
}
