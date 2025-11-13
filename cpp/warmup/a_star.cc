#include "cpp/warmup/a_star.hpp"

#include <cstdlib>
#include <queue>
#include <string>
#include <vector>

using std::abs;
using std::priority_queue;
using std::string;
using std::vector;

namespace warmup {

cell cell_left(const cell &c) {
  cell res{};
  res.x = c.x - 1;
  res.y = c.y;
  return res;
}

cell cell_right(const cell &c) {
  cell res{};
  res.x = c.x + 1;
  res.y = c.y;
  return res;
}

cell cell_up(const cell &c) {
  cell res{};
  res.x = c.x;
  res.y = c.y - 1;
  return res;
}

cell cell_down(const cell &c) {
  cell res{};
  res.x = c.x;
  res.y = c.y + 1;
  return res;
}

bool is_empty(const Maze &maze, const cell &c) {
  if (maze.empty() || c.x < 0 || c.y < 0 ||
      c.x >= static_cast<int>(maze[0].size()) ||
      c.y >= static_cast<int>(maze.size())) {
    return false;
  }
  return maze[c.y][c.x] == '.';
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

bool a_star_search(Maze &maze, const cell &start, const cell &end) {
  if (maze.empty()) {
    return false;
  }
  if (!is_empty(maze, end)) {
    return false;
  }

  CompareDistance const cf(end);

  priority_queue<cell, vector<cell>, CompareDistance> a_star_heap(cf);
  a_star_heap.push(start);

  while (!a_star_heap.empty()) {
    auto v = a_star_heap.top();
    a_star_heap.pop();

    if (!is_empty(maze, v)) {
      continue;
    }

    if (end.x == v.x && end.y == v.y) {
      return true;
    }

    maze[v.y][v.x] = 'V';
    a_star_heap.push(cell_left(v));
    a_star_heap.push(cell_right(v));
    a_star_heap.push(cell_up(v));
    a_star_heap.push(cell_down(v));
  }

  return false;
}

}  // namespace warmup
