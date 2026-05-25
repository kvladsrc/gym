#include <algorithm>
#include <iostream>
#include <map>
#include <vector>

using std::map;
using std::vector;

static int get_majority_element(vector<int>& a) {
  map<int, int> m;
  for (int const i : a) {
    m[i]++;
  }

  auto it = std::find_if(m.begin(), m.end(), [&a](const auto& p) {
    return p.second > static_cast<int>(a.size()) / 2;
  });

  return it != m.end() ? 1 : -1;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (int& i : a) {
    std::cin >> i;
  }
  std::cout << (get_majority_element(a) != -1) << '\n';
}
