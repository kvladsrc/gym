#include <iostream>
#include <string>

using std::min;
using std::string;

struct response {
  int val{0};
  int free_open{0};
  int free_close{0};
  response() = default;
  void merge(response left, response right) {
    auto valid = min(left.free_open, right.free_close);
    val += valid + left.val + right.val;
    free_open = left.free_open + right.free_open - valid;
    free_close = left.free_close + right.free_close - valid;
  }
};

struct node {
  node *left;
  node *right;
  int val;
  int free_open;
  int free_close;

  node() : left(nullptr), right(nullptr), val(0), free_open(0), free_close(0) {}
  explicit node(int aval)
      : left(nullptr), right(nullptr), val(aval), free_open(0), free_close(0) {}
  void merge() {
    val = left->val + right->val;
    auto valid = min(left->free_open, right->free_close);
    val += valid;
    free_open = left->free_open + right->free_open - valid;
    free_close = left->free_close + right->free_close - valid;
  }

  response query(int l, int r, int cur_l, int cur_r) const {
    response res;

    if (r < cur_l || l > cur_r) {
      return res;
    }

    if (l <= cur_l && r >= cur_r) {
      res.free_close = free_close;
      res.free_open = free_open;
      res.val = val;
      return res;
    }

    int const mid = (cur_l + cur_r) / 2;
    auto res_l = left->query(l, r, cur_l, mid);
    auto res_r = right->query(l, r, mid + 1, cur_r);
    res.merge(res_l, res_r);
    return res;
  }

  void build(string &s, int l, int r) {
    if (l == r) {
      if (s[l] == '(') {
        free_open = 1;
      } else {
        free_close = 1;
      }

      return;
    }

    int const mid = (l + r) / 2;

    left = new node();
    left->build(s, l, mid);
    right = new node();
    right->build(s, mid + 1, r);

    merge();
  }
};

// Read a string consists from '(' and ')' chars, 0 <= q <= 100 --
// number of queries, then q pairs 1 <= l <= r <= s.size() for every
// queiry need to calculate a number of correct bracers in substring
// l..r. Examples:
//
// ))(( -- 0 correct sequences;
// ()() -- 4 correct sequences;
// (() -- 2 correct sequence;
// (()())())) -- 8.
int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;
  auto *root = new node();
  root->build(s, 0, s.size() - 1);

  int q = 0;
  std::cin >> q;
  while ((q--) != 0) {
    int l = 0;
    int r = 0;
    std::cin >> l >> r;
    l--;
    r--;
    auto res = root->query(l, r, 0, s.size() - 1);
    std::cout << res.val * 2 << "\n";
  }

  return 0;
}
