#include <cstdlib>
#include <iostream>
#include <utility>
#include <vector>

using std::swap;
using std::vector;

static int partition2(vector<int>& a, int l, int r) {
  int const x = a[l];
  int j = l;
  for (int i = l + 1; i <= r; i++) {
    if (a[i] <= x) {
      j++;
      swap(a[i], a[j]);
    }
  }
  swap(a[l], a[j]);
  return j;
}

static void randomized_quick_sort(vector<int>& a, int l, int r,
                                  unsigned int* seed) {
  if (l >= r) {
    return;
  }

  int const k = l + (rand_r(seed) % (r - l + 1));
  swap(a[l], a[k]);
  int const m_e = partition2(a, l, r);
  int m_s = m_e;
  while ((m_s - 1) >= l && a[m_e] == a[m_s - 1]) {
    m_s--;
  }

  if (m_s > l) {
    randomized_quick_sort(a, l, m_s - 1, seed);
  }

  randomized_quick_sort(a, m_e + 1, r, seed);
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (int& i : a) {
    std::cin >> i;
  }
  unsigned int seed = 42;
  randomized_quick_sort(a, 0, a.size() - 1, &seed);
  for (int const i : a) {
    std::cout << i << ' ';
  }
}
