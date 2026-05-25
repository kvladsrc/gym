#include <algorithm>
#include <iostream>
#include <vector>

using std::vector;

struct Pair {
  int a;
  int b;
};

bool Compare(const Pair& a, const Pair& b) { return a.b < b.b; }

int lcs(const vector<int>& a, const vector<int>& b) {
  vector<vector<int>> table;
  table.resize(a.size() + 1, vector<int>(b.size() + 1));

  for (size_t i = 0; i <= a.size(); ++i) {
    table[i][0] = 0;
  }

  for (size_t i = 0; i <= b.size(); ++i) {
    table[0][i] = 0;
  }

  for (size_t i = 1; i <= a.size(); ++i) {
    for (size_t j = 1; j <= b.size(); ++j) {
      if (a[i - 1] == b[j - 1]) {
        table[i][j] = table[i - 1][j - 1] + 1;
      } else {
        if (table[i][j - 1] > table[i - 1][j]) {
          table[i][j] = table[i][j - 1];
        } else {
          table[i][j] = table[i - 1][j];
        }
      }
    }
  }

  return table[a.size()][b.size()];
}

int main() {
  size_t n;
  std::cin >> n;
  vector<Pair> p(n);
  vector<int> a(n);

  for (size_t i = 0; i < n; i++) {
    std::cin >> p[i].a >> p[i].b;
    a[i] = p[i].a;
  }

  vector<int> b(a);
  std::sort(a.begin(), a.end());
  std::sort(p.begin(), p.end(), Compare);
  for (size_t i = 0; i < n; i++) {
    b[i] = p[i].a;
  }

  std::cout << lcs(a, b) << std::endl;
}
