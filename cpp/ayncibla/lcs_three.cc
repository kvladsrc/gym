#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

struct Result {
  bool calculated;
  int lcs;
};

static int lcs3(vector<int>& a, size_t a_pos, vector<int>& b, size_t b_pos,
                vector<int>& c, size_t c_pos, Result*** table) {
  if (a_pos == a.size() || b_pos == b.size() || c_pos == c.size()) {
    return 0;
  }

  if (table[a_pos][b_pos][c_pos].calculated) {
    return table[a_pos][b_pos][c_pos].lcs;
  }

  int const case1 = lcs3(a, a_pos + 1, b, b_pos, c, c_pos, table);
  int const case2 = lcs3(a, a_pos, b, b_pos + 1, c, c_pos, table);
  int const case3 = lcs3(a, a_pos, b, b_pos, c, c_pos + 1, table);

  int tmp_max = case1 > case2 ? case1 : case2;
  tmp_max = tmp_max > case3 ? tmp_max : case3;

  if (a[a_pos] == b[b_pos] && b[b_pos] == c[c_pos]) {
    int const case4 = 1 + lcs3(a, a_pos + 1, b, b_pos + 1, c, c_pos + 1, table);
    table[a_pos][b_pos][c_pos].lcs = tmp_max > case4 ? tmp_max : case4;
    table[a_pos][b_pos][c_pos].calculated = true;
    return table[a_pos][b_pos][c_pos].lcs;
  }

  table[a_pos][b_pos][c_pos].lcs = tmp_max;
  table[a_pos][b_pos][c_pos].calculated = true;
  return table[a_pos][b_pos][c_pos].lcs;
}

int main() {
  size_t an = 0;
  std::cin >> an;
  vector<int> a(an);
  for (size_t i = 0; i < an; i++) {
    std::cin >> a[i];
  }
  size_t bn = 0;
  std::cin >> bn;
  vector<int> b(bn);
  for (size_t i = 0; i < bn; i++) {
    std::cin >> b[i];
  }
  size_t cn = 0;
  std::cin >> cn;
  vector<int> c(cn);
  for (size_t i = 0; i < cn; i++) {
    std::cin >> c[i];
  }

  auto* table = new Result**[an];
  for (size_t i = 0; i < an; ++i) {
    table[i] = new Result*[bn];
    for (size_t j = 0; j < bn; ++j) {
      table[i][j] = new Result[cn];
    }
  }

  std::cout << lcs3(a, 0, b, 0, c, 0, table) << '\n';
}
