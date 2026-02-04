#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

// Over-over complicated solution. Here is the simplest one:
//
// string s;
// std::cin >> s;
//
// vector<int> pref_sum(s.size() + 1);
// pref_sum.front() = 0;
// for (size_t idx = 0; (idx + 1) < s.size(); ++idx) {
//   pref_sum[idx + 1] = pref_sum[idx];
//   if (s[idx] == s[idx + 1]) {
//     pref_sum[idx + 1]++;
//   }
// }
//
// pref_sum.back() = pref_sum[s.size() - 1];
// int m;
// std::cin >> m;
// while (m--) {
//   int l, r;
//   std::cin >> l >> r;
//
//   std::cout << pref_sum[r - 1] - pref_sum[l - 1] << "\n";
// }

struct Node {
  int num;
  Node *left, *right;
};

void build(Node* root, int l, int r, string& in) {
  if (r == l) {
    root->num = 0;
    return;
  }

  root->left = new Node;
  root->right = new Node;

  int mid = (r + l) / 2;
  build(root->left, l, mid, in);
  build(root->right, mid + 1, r, in);

  root->num = root->left->num + root->right->num;
  if (in[mid] == in[mid + 1]) root->num++;
}

int query(Node* root, int l, int r, int cur_l, int cur_r, string& in) {
  if (r <= cur_l || l >= cur_r) {
    return 0;
  }

  if (r >= cur_r && l <= cur_l) {
    return root->num;
  }

  int mid = (cur_l + cur_r) / 2;
  int res = 0;
  res += query(root->left, l, r, cur_l, mid, in);
  res += query(root->right, l, r, mid + 1, cur_r, in);
  if (l <= mid && r >= mid + 1) {
    if (in[mid] == in[mid + 1]) res++;
  }
  return res;
}

int main(int argc, char* argv[]) {
  string in;
  std::cin >> in;

  Node root;
  build(&root, 0, in.size() - 1, in);

  int t;
  std::cin >> t;
  while (t--) {
    int l, r;
    std::cin >> l >> r;
    std::cout << query(&root, l - 1, r - 1, 0, in.size() - 1, in) << "\n";
  }

  return 0;
}
