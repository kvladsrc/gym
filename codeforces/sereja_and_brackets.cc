#include <algorithm>
#include <iostream>
#include <stack>
#include <string>
#include <vector>

using std::min;
using std::pair;
using std::sort;
using std::stack;
using std::string;
using std::vector;

struct Node {
  int o, c, t;

  Node operator+(const Node &other) {
    Node result;
    int tmp = min(o, other.c);
    result.t = t + other.t + tmp * 2;
    result.o = o + other.o - tmp;
    result.c = c + other.c - tmp;
    return result;
  }
};

// T(n) = 2T(n/2) + 1
// O(n log n)
void build(int id, int l, int r, vector<Node> &tree, string &s) {
  if (r == l) {
    if (s[l] == '(')
      tree[id].o++;
    else
      tree[id].c++;
    return;
  }

  int mid = (l + r) / 2;
  build(2 * id, l, mid, tree, s);
  build(2 * id + 1, mid + 1, r, tree, s);

  tree[id] = tree[2 * id] + tree[2 * id + 1];
}

// O(n log n)
Node query(vector<Node> &tree, int id, int x, int y, int l, int r) {
  if (l > y || x > r) return Node();
  if (x <= l && r <= y) return tree[id];

  int mid = (l + r) / 2;

  Node a = query(tree, 2 * id, x, y, l, mid),
       b = query(tree, 2 * id + 1, x, y, mid + 1, r);

  return a + b;
}

int main() {
  string s;
  std::cin >> s;
  int m;
  std::cin >> m;
  vector<int> l(m);
  vector<int> r(m);
  for (int i = 0; i < m; ++i) {
    std::cin >> l[i];
    std::cin >> r[i];
  }

  vector<Node> tree(s.size() * 4);
  build(1, 0, s.size() - 1, tree, s);

  for (int i = 0; i < m; ++i) {
    std::cout << query(tree, 1, l[i] - 1, r[i] - 1, 0, s.size() - 1).t << "\n";
  }
}
