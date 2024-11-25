#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

#define INF 1000000000

int device(int n, int m, int actions, unordered_map<int, int> &visited) {
  if (visited[n] && visited[n] < actions) return INF;
  visited[n] = actions;

  if (n == m) return actions;
  if (n > m) return actions + n - m;
  if (n <= 0) return INF;
  int case1 = device(n - 1, m, actions + 1, visited);
  int case2 = device(n * 2, m, actions + 1, visited);
  return min(case1, case2);
}

// Another aproach is to use a BFS to find the shortest path from n to
// m. This is a more efficient approach, but the previous one is more
// elegant.
int device_bfs(int n, int m) {
  queue<pair<int, int>> q;
  q.push({n, 0});
  while (!q.empty()) {
    pair<int, int> p = q.front();
    q.pop();
    if (p.first == m) return p.second;
    if (p.first > m) {
      q.push({p.first - 1, p.second + 1});
    } else {
      q.push({p.first * 2, p.second + 1});
      q.push({p.first - 1, p.second + 1});
    }
  }
  return -1;
}

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  unordered_map<int, int> visited;
  std::cout << device(n, m, 0, visited) << "\n";

  return 0;
}
