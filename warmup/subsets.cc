#include <chrono>
#include <cstdint>
#include <iostream>
#include <vector>

#define empty_set vector<int>(0)

using std::vector;

vector<vector<int>> subsets_rec(const vector<int> &a) {
  vector<vector<int>> res;
  if (a.size() == 0)
    return res;

  if (a.size() == 1) {
    res.push_back({a[0]});
    res.push_back({});
    return res;
  }

  vector<int> rest(a.begin() + 1, a.end());
  vector<vector<int>> b = subsets_rec(rest);

  for (vector<int> s : b) {
    vector<int> with_elem(s);
    with_elem.push_back(a[0]);
    res.push_back(s);
    res.push_back(with_elem);
  }

  return res;
}

int64_t fast_power(int x, int y, int modulo) {
  if (y == 1)
    return x;
  if (y == 0)
    return 1;

  // x^y = (x^2)^y/2
  int64_t buf;
  if (y % 2) {
    buf = fast_power(x, y - 1, modulo);
    buf *= x;
  } else {
    buf = fast_power(x * x, y / 2, modulo);
  }

  buf %= modulo;
  return buf;
}

vector<vector<int>> subsets_enumerate(int size) {
  const int64_t modulo = 1000000000;

  // Ignore the empty set.
  int64_t res_size = fast_power(2, size, modulo) - 1;
  vector<vector<int>> res(res_size);

  for (int i = 1; i <= res_size; ++i) {
    vector<int> elem;
    int i_cpy = i, pos = 1;

    while (i_cpy) {
      if (i_cpy % 2) {
        elem.push_back(pos);
      }
      pos++;
      i_cpy /= 2;
    }
    res[i - 1] = elem;
  }

  return res;
}

int main() {
  int n;
  std::cin >> n;

  auto start_enum = std::chrono::high_resolution_clock::now();
  auto ss = subsets_enumerate(n);
  auto end_enum = std::chrono::high_resolution_clock::now();
  auto duration_enum =
      std::chrono::duration_cast<std::chrono::seconds>(end_enum - start_enum);

  vector<int> for_rec(n);
  for (int i = 1; i <= n; ++i) {
    for_rec[i] = i;
  }

  auto start_rec = std::chrono::high_resolution_clock::now();
  auto ss_rec = subsets_enumerate(n);
  auto end_rec = std::chrono::high_resolution_clock::now();
  auto duration_rec =
      std::chrono::duration_cast<std::chrono::seconds>(end_rec - start_rec);

  std::cout << "Rec duration: " << duration_rec.count() << "\n";
  std::cout << "Enum duration: " << duration_enum.count() << "\n";

  return 0;
}
